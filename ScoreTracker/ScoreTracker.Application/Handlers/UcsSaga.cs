using MassTransit;
using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Handlers
{
    public sealed class UcsSaga
        : IRequestHandler<RegisterUcsEntryCommand>,
            IRequestHandler<CreateUcsChartCommand>,
            IRequestHandler<AddUcsChartTagCommand>,
            IRequestHandler<DeleteUcsChartTagCommand>,
            IRequestHandler<GetUcsChartsQuery, IEnumerable<UcsChart>>,
            IRequestHandler<GetUcsChartLeaderboardQuery, IEnumerable<UcsLeaderboardEntry>>,
            IRequestHandler<GetUcsChartTagsQuery, IEnumerable<ChartTagAggregate>>,
            IRequestHandler<GetMyUcsChartTagsQuery, IEnumerable<Name>>,
            IRequestHandler<GetAllMyUcsChartTagsQuery, IEnumerable<UserChartTag>>
    {
        private readonly IBus _bus;
        private readonly IUcsRepository _ucsRepository;
        private readonly ICurrentUserAccessor _currentUserAccessor;
        private readonly IDateTimeOffsetAccessor _dateTimeOffset;

        public UcsSaga(IBus bus, IUcsRepository ucsRepository,
            ICurrentUserAccessor currentUserAccessor,
            IDateTimeOffsetAccessor dateTimeOffset)
        {
            _bus = bus;
            _ucsRepository = ucsRepository;
            _currentUserAccessor = currentUserAccessor;
            _dateTimeOffset = dateTimeOffset;
        }

        public async Task Handle(RegisterUcsEntryCommand request, CancellationToken cancellationToken)
        {
            await _ucsRepository.UpdateScore(request.ChartId, _currentUserAccessor.User.Id, request.Score,
                request.Plate, request.IsBroken, request.VideoPath, request.ImagePath, cancellationToken);
            var chart = (await _ucsRepository.GetUcsCharts(cancellationToken))
                .Single(c => c.Chart.Id == request.ChartId);
            // Plate travels as the enum name (matches the Ledger contract events' format).
            await _bus.Publish(UcsLeaderboardPlacedEvent.Create(_dateTimeOffset.Now,
                    _currentUserAccessor.User.Id, request.ChartId, request.Score, request.Plate.ToString(),
                    request.IsBroken, chart.Artist, chart.Chart.Song.Name, chart.Chart.DifficultyString),
                cancellationToken);
        }

        public async Task Handle(CreateUcsChartCommand request, CancellationToken cancellationToken)
        {
            await _ucsRepository.CreateUcsChart(request.Chart, cancellationToken);
        }

        public async Task Handle(AddUcsChartTagCommand request, CancellationToken cancellationToken)
        {
            await _ucsRepository.AddChartTag(request.ChartId, _currentUserAccessor.User.Id, request.Tag,
                cancellationToken);
        }

        public async Task Handle(DeleteUcsChartTagCommand request, CancellationToken cancellationToken)
        {
            await _ucsRepository.DeleteChartTag(request.ChartId, _currentUserAccessor.User.Id, request.Tag,
                cancellationToken);
        }

        public async Task<IEnumerable<UcsChart>> Handle(GetUcsChartsQuery request,
            CancellationToken cancellationToken)
        {
            return await _ucsRepository.GetUcsCharts(cancellationToken);
        }

        public async Task<IEnumerable<UcsLeaderboardEntry>> Handle(GetUcsChartLeaderboardQuery request,
            CancellationToken cancellationToken)
        {
            return await _ucsRepository.GetChartLeaderboard(request.ChartId, cancellationToken);
        }

        public async Task<IEnumerable<ChartTagAggregate>> Handle(GetUcsChartTagsQuery request,
            CancellationToken cancellationToken)
        {
            return await _ucsRepository.GetChartTags(cancellationToken);
        }

        public async Task<IEnumerable<Name>> Handle(GetMyUcsChartTagsQuery request,
            CancellationToken cancellationToken)
        {
            return await _ucsRepository.GetMyTags(request.ChartId, _currentUserAccessor.User.Id, cancellationToken);
        }

        public async Task<IEnumerable<UserChartTag>> Handle(GetAllMyUcsChartTagsQuery request,
            CancellationToken cancellationToken)
        {
            return await _ucsRepository.GetAllMyTags(_currentUserAccessor.User.Id, cancellationToken);
        }
    }
}
