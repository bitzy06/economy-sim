using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;

namespace Economy_sim
{
    public class TradeMenuViewModel : INotifyPropertyChanged
    {
        private readonly Func<bool, Task> _createTradeCallback;

        public ObservableCollection<string> Exports { get; } = new();
        public ObservableCollection<string> Imports { get; } = new();
        public ObservableCollection<string> RecentTradeEvents { get; } = new();
        public ObservableCollection<string> Aggregates { get; } = new();

        private string _tradeSummaryText = "No trade data available.";
        public string TradeSummaryText
        {
            get => _tradeSummaryText;
            private set
            {
                if (_tradeSummaryText != value)
                {
                    _tradeSummaryText = value;
                    OnPropertyChanged(nameof(TradeSummaryText));
                }
            }
        }

        public ICommand CreateExportCommand { get; }
        public ICommand CreateImportCommand { get; }

        public TradeMenuViewModel(Func<bool, Task> createTradeCallback)
        {
            _createTradeCallback = createTradeCallback;
            CreateExportCommand = new AsyncRelayCommand(_ => _createTradeCallback(true));
            CreateImportCommand = new AsyncRelayCommand(_ => _createTradeCallback(false));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Refresh(GlobalMarket? market, TradeRouteManager? tradeRouteManager, EnhancedTradeManager? enhancedTradeManager, string? focusCountry)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => Refresh(market, tradeRouteManager, enhancedTradeManager, focusCountry));
                return;
            }

            UpdateCollection(Exports, BuildTradeFlowStrings(market?.GetTopExports(focusCountry ?? string.Empty, 6)));
            UpdateCollection(Imports, BuildTradeFlowStrings(market?.GetTopImports(focusCountry ?? string.Empty, 6)));
            UpdateCollection(RecentTradeEvents, BuildRecentEventStrings(market));
            UpdateCollection(Aggregates, BuildAggregates(market, tradeRouteManager, enhancedTradeManager, focusCountry));

            TradeSummaryText = BuildSummaryText(market, focusCountry);
        }

        private static IEnumerable<string> BuildTradeFlowStrings(IEnumerable<KeyValuePair<string, int>>? flows)
        {
            if (flows == null)
                yield break;

            foreach (var flow in flows)
            {
                yield return $"{flow.Key}: {flow.Value:N0} units";
            }
        }

        private static IEnumerable<string> BuildRecentEventStrings(GlobalMarket? market)
        {
            if (market?.RecentTradeEvents == null || market.RecentTradeEvents.Count == 0)
            {
                yield break;
            }

            foreach (var evt in market.RecentTradeEvents.Take(6))
            {
                var direction = string.IsNullOrWhiteSpace(evt.ExportingCountry) || string.IsNullOrWhiteSpace(evt.ImportingCountry)
                    ? "Trade"
                    : $"{evt.ExportingCountry} → {evt.ImportingCountry}";
                yield return $"{direction}: {evt.Quantity:N0} {evt.GoodName} ({evt.TotalValue:C0})";
            }
        }

        private static IEnumerable<string> BuildAggregates(GlobalMarket? market, TradeRouteManager? tradeRouteManager, EnhancedTradeManager? enhancedTradeManager, string? focusCountry)
        {
            var aggregates = new List<string>();

            if (market != null)
            {
                aggregates.Add($"Global Trade Value: {market.GlobalTradeValue:C0}");
            }

            if (tradeRouteManager != null)
            {
                var routes = tradeRouteManager.AllTradeRoutes;
                var totalCapacity = routes.Sum(r => r.Capacity);
                var totalUsage = routes.Sum(r => r.CurrentUsage);
                aggregates.Add($"Trade Routes: {routes.Count} active");
                aggregates.Add($"Route Utilisation: {totalUsage:N0}/{totalCapacity:N0} units");
            }

            if (enhancedTradeManager != null)
            {
                var relevantDeals = enhancedTradeManager.EnhancedTradeAgreements
                    .Where(a => a.Status == TradeStatus.Active)
                    .Where(a => string.IsNullOrWhiteSpace(focusCountry) || a.ParticipatingCountries.Contains(focusCountry!))
                    .ToList();

                aggregates.Add($"Active Agreements: {relevantDeals.Count}");
            }

            return aggregates;
        }

        private static string BuildSummaryText(GlobalMarket? market, string? focusCountry)
        {
            if (market == null || string.IsNullOrWhiteSpace(focusCountry) || !market.CountryTradeFlows.TryGetValue(focusCountry, out var flows))
            {
                return "No trade data available.";
            }

            var totalExports = flows.Values.Sum(f => f.Exports);
            var totalImports = flows.Values.Sum(f => f.Imports);

            string balanceText = totalExports >= totalImports
                ? $"Trade surplus of {totalExports - totalImports:N0} units"
                : $"Trade deficit of {totalImports - totalExports:N0} units";

            return $"{focusCountry} exports {totalExports:N0} units and imports {totalImports:N0} units ({balanceText}).";
        }

        private void UpdateCollection(ObservableCollection<string> target, IEnumerable<string> values)
        {
            target.Clear();
            foreach (var value in values)
            {
                target.Add(value);
            }
        }

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private sealed class AsyncRelayCommand : ICommand
        {
            private readonly Func<object?, Task> _execute;
            private readonly Predicate<object?>? _canExecute;

            public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

            public async void Execute(object? parameter)
            {
                await _execute(parameter);
            }
        }
    }
}
