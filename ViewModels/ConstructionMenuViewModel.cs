using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Threading;

namespace Economy_sim
{
    public class ConstructionMenuViewModel : INotifyPropertyChanged
    {
        private readonly ObservableCollection<ConstructionProjectDisplay> activeProjects = new();
        private readonly Dictionary<ConstructionProject, ConstructionProjectDisplay> displayLookup = new();
        private readonly ObservableCollection<ConstructionCompanyOption> companyOptions = new();
        private readonly RelayCommand buildFactoryCommand;
        private readonly RelayCommand buildRoadCommand;
        private readonly RelayCommand buildBridgeCommand;
        private readonly RelayCommand buildPortCommand;
        private readonly RelayCommand buildAirportCommand;
        private readonly Dictionary<ProjectType, ConstructionProjectConfig> projectConfigs;

        private City? city;
        private ConstructionCompanyOption? selectedCompanyOption;
        private ConstructionProjectDisplay? selectedProject;
        private double cityBudget;
        private decimal totalAllocatedBudget;
        private decimal estimatedDailyCost;
        private ConstructionProject? pendingFocusProject;

        public ConstructionMenuViewModel()
        {
            projectConfigs = CreateDefaultConfigs();

            buildFactoryCommand = new RelayCommand(_ => QueueProject(ProjectType.Factory), _ => CanQueueProject(ProjectType.Factory));
            buildRoadCommand = new RelayCommand(_ => QueueProject(ProjectType.Road), _ => CanQueueProject(ProjectType.Road));
            buildBridgeCommand = new RelayCommand(_ => QueueProject(ProjectType.Bridge), _ => CanQueueProject(ProjectType.Bridge));
            buildPortCommand = new RelayCommand(_ => QueueProject(ProjectType.Port), _ => CanQueueProject(ProjectType.Port));
            buildAirportCommand = new RelayCommand(_ => QueueProject(ProjectType.Airport), _ => CanQueueProject(ProjectType.Airport));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<ConstructionProjectDisplay> ActiveProjects => activeProjects;

        public ObservableCollection<ConstructionCompanyOption> CompanyOptions => companyOptions;

        public ICommand BuildFactoryCommand => buildFactoryCommand;

        public ICommand BuildRoadCommand => buildRoadCommand;

        public ICommand BuildBridgeCommand => buildBridgeCommand;

        public ICommand BuildPortCommand => buildPortCommand;

        public ICommand BuildAirportCommand => buildAirportCommand;

        public ConstructionCompanyOption? SelectedCompanyOption
        {
            get => selectedCompanyOption;
            set
            {
                if (selectedCompanyOption != value)
                {
                    selectedCompanyOption = value;
                    OnPropertyChanged(nameof(SelectedCompanyOption));
                    UpdateCommandStates();
                }
            }
        }

        public ConstructionProjectDisplay? SelectedProject
        {
            get => selectedProject;
            set
            {
                if (selectedProject != value)
                {
                    selectedProject = value;
                    OnPropertyChanged(nameof(SelectedProject));
                    OnPropertyChanged(nameof(SelectedProjectSummary));
                }
            }
        }

        public double CityBudget
        {
            get => cityBudget;
            private set
            {
                if (Math.Abs(cityBudget - value) > 0.5)
                {
                    cityBudget = value;
                    OnPropertyChanged(nameof(CityBudget));
                    OnPropertyChanged(nameof(CityBudgetDisplay));
                }
            }
        }

        public string CityBudgetDisplay => $"City Budget: {CityBudget:C0}";

        public decimal TotalAllocatedBudget
        {
            get => totalAllocatedBudget;
            private set
            {
                if (totalAllocatedBudget != value)
                {
                    totalAllocatedBudget = value;
                    OnPropertyChanged(nameof(TotalAllocatedBudget));
                    OnPropertyChanged(nameof(TotalAllocatedBudgetDisplay));
                }
            }
        }

        public string TotalAllocatedBudgetDisplay => $"Allocated to Projects: {TotalAllocatedBudget:C0}";

        public decimal EstimatedDailyCost
        {
            get => estimatedDailyCost;
            private set
            {
                if (estimatedDailyCost != value)
                {
                    estimatedDailyCost = value;
                    OnPropertyChanged(nameof(EstimatedDailyCost));
                    OnPropertyChanged(nameof(EstimatedDailyCostDisplay));
                }
            }
        }

        public string EstimatedDailyCostDisplay => $"Estimated Daily Cost: {EstimatedDailyCost:C0}";

        public string SelectedProjectSummary => SelectedProject?.Summary ?? "Select a project to view details";

        public void BindToCity(City? targetCity, IEnumerable<ConstructionCompany>? companies)
        {
            city = targetCity;
            UpdateCompanies(companies);
            Refresh();
        }

        public void Refresh()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(Refresh);
                return;
            }

            CityBudget = city?.Budget ?? 0;
            TotalAllocatedBudget = city?.ActiveProjects.Sum(p => p.Budget) ?? 0m;
            EstimatedDailyCost = city?.ActiveProjects.Sum(p => CalculateDailyCost(p)) ?? 0m;

            SyncActiveProjects();

            UpdateCommandStates();
        }

        private void UpdateCommandStates()
        {
            buildFactoryCommand.RaiseCanExecuteChanged();
            buildRoadCommand.RaiseCanExecuteChanged();
            buildBridgeCommand.RaiseCanExecuteChanged();
            buildPortCommand.RaiseCanExecuteChanged();
            buildAirportCommand.RaiseCanExecuteChanged();
        }

        private void QueueProject(ProjectType type)
        {
            if (city == null || !projectConfigs.TryGetValue(type, out var config))
            {
                return;
            }

            var project = new ConstructionProject(type, config.Budget, config.Duration, config.Output, config.RequiredResource, config.ResourcePerDay);

            bool assignedToCompany = false;
            var company = SelectedCompanyOption?.Company;
            if (company != null)
            {
                assignedToCompany = company.TakeProject(project, city);
                if (!assignedToCompany)
                {
                    project.AssignedCompany = null;
                }
            }

            city.StartConstructionProject(project);

            if (!assignedToCompany && company != null && company.Projects.Contains(project))
            {
                company.Projects.Remove(project);
            }

            pendingFocusProject = project;
            Refresh();
            pendingFocusProject = null;

            if (displayLookup.TryGetValue(project, out var display))
            {
                SelectedProject = display;
            }
        }

        private bool CanQueueProject(ProjectType type)
        {
            if (city == null || !projectConfigs.TryGetValue(type, out var config))
            {
                return false;
            }

            if (SelectedCompanyOption?.Company != null)
            {
                return city.Budget >= (double)config.Budget;
            }

            var dailyCost = CalculateDailyCost(config);
            return city.Budget >= (double)dailyCost;
        }

        private void UpdateCompanies(IEnumerable<ConstructionCompany>? companies)
        {
            var previouslySelectedCompany = selectedCompanyOption?.Company;
            companyOptions.Clear();
            companyOptions.Add(new ConstructionCompanyOption("City Works Department", null));

            if (companies != null)
            {
                foreach (var company in companies)
                {
                    if (company == null)
                    {
                        continue;
                    }

                    companyOptions.Add(new ConstructionCompanyOption(company.Name, company));
                }
            }

            var newSelection = companyOptions.FirstOrDefault(option => option.Company == previouslySelectedCompany);
            if (newSelection == null)
            {
                newSelection = companyOptions.FirstOrDefault();
            }

            if (!ReferenceEquals(selectedCompanyOption, newSelection))
            {
                SelectedCompanyOption = newSelection;
            }
        }

        private void SyncActiveProjects()
        {
            foreach (var kvp in displayLookup.ToList())
            {
                if (city?.ActiveProjects.Contains(kvp.Key) != true)
                {
                    kvp.Value.PropertyChanged -= OnProjectDisplayPropertyChanged;
                    activeProjects.Remove(kvp.Value);
                    displayLookup.Remove(kvp.Key);
                    if (SelectedProject == kvp.Value)
                    {
                        SelectedProject = null;
                    }
                }
            }

            if (city?.ActiveProjects == null)
            {
                if (SelectedProject == null && activeProjects.Count > 0)
                {
                    SelectedProject = activeProjects[0];
                }
                return;
            }

            for (int i = 0; i < city.ActiveProjects.Count; i++)
            {
                var project = city.ActiveProjects[i];
                if (!displayLookup.TryGetValue(project, out var display))
                {
                    projectConfigs.TryGetValue(project.Type, out var config);
                    display = new ConstructionProjectDisplay(project, city.Name, config);
                    display.PropertyChanged += OnProjectDisplayPropertyChanged;
                    displayLookup[project] = display;
                    if (i <= activeProjects.Count)
                    {
                        activeProjects.Insert(i, display);
                    }
                    else
                    {
                        activeProjects.Add(display);
                    }
                }
                else
                {
                    display.Refresh();
                    var currentIndex = activeProjects.IndexOf(display);
                    if (currentIndex != i)
                    {
                        activeProjects.Move(currentIndex, i);
                    }
                }

                if (pendingFocusProject == project)
                {
                    SelectedProject = display;
                }
            }

            if (SelectedProject == null && activeProjects.Count > 0)
            {
                SelectedProject = activeProjects[0];
            }
        }

        private void OnProjectDisplayPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender == SelectedProject && (e.PropertyName == nameof(ConstructionProjectDisplay.Summary) || e.PropertyName == nameof(ConstructionProjectDisplay.ProgressPercentage)))
            {
                OnPropertyChanged(nameof(SelectedProjectSummary));
            }
        }

        private static decimal CalculateDailyCost(ConstructionProject project)
        {
            return project.Duration > 0 ? project.Budget / project.Duration : 0m;
        }

        private static decimal CalculateDailyCost(ConstructionProjectConfig config)
        {
            return config.Duration > 0 ? config.Budget / config.Duration : 0m;
        }

        private static Dictionary<ProjectType, ConstructionProjectConfig> CreateDefaultConfigs()
        {
            return new Dictionary<ProjectType, ConstructionProjectConfig>
            {
                [ProjectType.Factory] = new ConstructionProjectConfig(ProjectType.Factory, "Industrial Factory", "Boost city manufacturing capacity and jobs.", 500_000m, 180, 1.0, "Machine Parts", 2),
                [ProjectType.Road] = new ConstructionProjectConfig(ProjectType.Road, "Major Roadway", "Improve local transport throughput and reduce congestion.", 120_000m, 120, 15.0, "Cement", 5),
                [ProjectType.Bridge] = new ConstructionProjectConfig(ProjectType.Bridge, "River Bridge", "Connect districts separated by rivers to increase trade.", 220_000m, 150, 2.5, "Steel", 4),
                [ProjectType.Port] = new ConstructionProjectConfig(ProjectType.Port, "Commercial Port", "Expand shipping capacity for imports and exports.", 350_000m, 200, 1.0, "Lumber", 6),
                [ProjectType.Airport] = new ConstructionProjectConfig(ProjectType.Airport, "Regional Airport", "Open the city to rapid passenger and cargo travel.", 600_000m, 260, 1.0, "Refined Oil", 3)
            };
        }

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public sealed record ConstructionProjectConfig(ProjectType Type, string DisplayName, string Description, decimal Budget, int Duration, double Output, string RequiredResource, int ResourcePerDay);

        public sealed class ConstructionCompanyOption
        {
            public ConstructionCompanyOption(string displayName, ConstructionCompany? company)
            {
                DisplayName = displayName;
                Company = company;
            }

            public string DisplayName { get; }

            public ConstructionCompany? Company { get; }

            public override string ToString() => DisplayName;
        }

        public sealed class ConstructionProjectDisplay : INotifyPropertyChanged
        {
            private readonly ConstructionProject project;
            private readonly string cityName;
            private readonly ConstructionProjectConfig? config;

            public ConstructionProjectDisplay(ConstructionProject project, string cityName, ConstructionProjectConfig? config)
            {
                this.project = project;
                this.cityName = cityName;
                this.config = config;
            }

            public ConstructionProject Project => project;

            public string Title => $"{config?.DisplayName ?? project.Type.ToString()} — {cityName}";

            public double ProgressPercentage => project.Duration <= 0 ? 100 : Math.Clamp(project.Progress / (double)project.Duration * 100.0, 0, 100);

            public string ProgressText => project.Duration <= 0
                ? "Complete"
                : $"{project.Progress}/{project.Duration} days ({ProgressPercentage:F1}%)";

            public string BudgetDisplay => $"Budget: {project.Budget:C0}";

            public string BudgetRemainingDisplay => $"Remaining: {project.BudgetRemaining:C0}";

            public string AssignedCompanyDisplay => $"Company: {project.AssignedCompany?.Name ?? "City Works Department"}";

            public string Status => project.IsComplete() ? "Complete" : "In Progress";

            public decimal DailyCost => CalculateDailyCost(project);

            public string DailyCostDisplay => $"Daily Cost: {DailyCost:C0}";

            public string Summary
            {
                get
                {
                    var description = config?.Description ?? "Construction project";
                    return $"{description}\nStatus: {Status}\n{ProgressText}\n{DailyCostDisplay}\n{BudgetDisplay}\n{BudgetRemainingDisplay}\n{AssignedCompanyDisplay}";
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public void Refresh()
            {
                OnPropertyChanged(nameof(ProgressPercentage));
                OnPropertyChanged(nameof(ProgressText));
                OnPropertyChanged(nameof(BudgetRemainingDisplay));
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(Summary));
                OnPropertyChanged(nameof(AssignedCompanyDisplay));
                OnPropertyChanged(nameof(DailyCost));
                OnPropertyChanged(nameof(DailyCostDisplay));
            }

            private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private sealed class RelayCommand : ICommand
        {
            private readonly Action<object?> execute;
            private readonly Predicate<object?>? canExecute;

            public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
            {
                this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
                this.canExecute = canExecute;
            }

            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

            public void Execute(object? parameter) => execute(parameter);

            public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
