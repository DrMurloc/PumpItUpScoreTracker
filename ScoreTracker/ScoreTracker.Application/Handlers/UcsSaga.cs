using MassTransit;
using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class UcsSaga
        : IRequestHandler<RegisterUcsEntryCommand>
    {
        private readonly IBus _bus;
        private readonly IUcsRepository _ucsRepository;
        private readonly ICurrentUserAccessor _currentUserAccessor;

        public UcsSaga(IBus bus, IUcsRepository ucsRepository,
            ICurrentUserAccessor currentUserAccessor)
        {
            _bus = bus;
            _ucsRepository = ucsRepository;
            _currentUserAccessor = currentUserAccessor;
        }

        public async Task Handle(RegisterUcsEntryCommand request, CancellationToken cancellationToken)
        {
            await _ucsRepository.UpdateScore(request.ChartId, _currentUserAccessor.User.Id, request.Score,
                request.Plate, request.IsBroken, request.VideoPath, request.ImagePath, cancellationToken);
            await _bus.Publish(new UcsLeaderboardPlacedEvent(_currentUserAccessor.User.Id, request.ChartId),
                cancellationToken
            );
        }
    }
}
