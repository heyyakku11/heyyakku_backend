using Microsoft.EntityFrameworkCore;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Personality;
using Yakku.Application.Personality.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class LoginSnapshotReader : ILoginSnapshotReader
    {
        private readonly YakkuDbContext _context;
        private readonly IPersonalityRepository _personality;

        public LoginSnapshotReader(YakkuDbContext context, IPersonalityRepository personality)
        {
            _context = context;
            _personality = personality;
        }

        public async Task<LoginSnapshot> GetAsync(
            Guid userId,
            string? installationId,
            CancellationToken cancellationToken = default)
        {
            var cardTask = ReadCardAsync(userId, cancellationToken);
            var communityTask = _context.CommunityWaitlist
                .AsNoTracking()
                .AnyAsync(entry => entry.UserId == userId, cancellationToken);
            var deviceTask = string.IsNullOrWhiteSpace(installationId)
                ? Task.FromResult<DeviceRow?>(null)
                : ReadDeviceAsync(userId, installationId, cancellationToken);

            await Task.WhenAll(cardTask, communityTask, deviceTask);

            var card = await cardTask;
            if (card is null)
            {
                await _personality.EnsureCardAsync(userId, cancellationToken);
                card = await ReadCardAsync(userId, cancellationToken)
                    ?? throw PersonalityExceptions.CardNotFound();
            }

            var permission = NotificationPermissionStatus.Unknown;
            var pushAllowed = false;
            if (await deviceTask is { } device)
            {
                permission = device.NotificationPermission ?? NotificationPermissionStatus.Unknown;
                pushAllowed = device.IsPushAllowed;
            }

            return new LoginSnapshot(
                card.ShareCode,
                card.Status.ToString(),
                permission.ToString(),
                pushAllowed,
                await communityTask);
        }

        private Task<CardRow?> ReadCardAsync(Guid userId, CancellationToken cancellationToken)
        {
            return _context.PersonalityCards
                .AsNoTracking()
                .Where(card => card.UserId == userId)
                .Select(card => new CardRow(card.ShareCode, card.Status))
                .FirstOrDefaultAsync(cancellationToken);
        }

        private Task<DeviceRow?> ReadDeviceAsync(
            Guid userId,
            string installationId,
            CancellationToken cancellationToken)
        {
            return _context.Devices
                .AsNoTracking()
                .Where(item => item.UserId == userId && item.InstallationId == installationId)
                .Select(item => new DeviceRow(item.NotificationPermission, item.IsPushAllowed))
                .FirstOrDefaultAsync(cancellationToken);
        }

        private sealed record CardRow(string ShareCode, PersonalityCardStatus Status);

        private sealed record DeviceRow(NotificationPermissionStatus? NotificationPermission, bool IsPushAllowed);
    }
}