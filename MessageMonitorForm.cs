using System;
using System.Windows.Forms;
using StrategyGame;

namespace economy_sim
{
    public partial class MessageMonitorForm : Form
    {
        public MessageMonitorForm()
        {
            InitializeComponent();

            MessageBus.Instance.Subscribe<ConstructionCompletedEvent>(HandleEvent);
            MessageBus.Instance.Subscribe<TradeRecordedEvent>(HandleEvent);
            MessageBus.Instance.Subscribe<EconomyUpdatedEventData>(HandleEvent);
            MessageBus.Instance.Subscribe<PopulationUpdatedEventData>(HandleEvent);
            MessageBus.Instance.Subscribe<DistrictDestroyedEventData>(HandleEvent);
            MessageBus.Instance.Subscribe<MajorInfrastructureBuiltEventData>(HandleEvent);
            MessageBus.Instance.Subscribe<CityLODChangedEventData>(HandleEvent);
            MessageBus.Instance.Subscribe<CityStatusEventData>(HandleEvent);
            MessageBus.Instance.Subscribe<CityGenerationRequestEventData>(HandleEvent);
            MessageBus.Instance.Subscribe<CityGenerationCompletedEventData>(HandleEvent);
            MessageBus.Instance.Subscribe<CustomDebugEvent>(HandleEvent);
        }

        private void HandleEvent<T>(T evt)
        {
            if (!IsHandleCreated)
            {
                DebugLogger.Log($"MessageMonitorForm not yet created. Dropping event: {evt}");
                return;
            }

            BeginInvoke(() =>
            {
                listBoxEvents.Items.Insert(0, evt?.ToString());
                if (listBoxEvents.Items.Count > 100)
                    listBoxEvents.Items.RemoveAt(listBoxEvents.Items.Count - 1);
            });
        }

        private void ButtonPublish_Click(object? sender, EventArgs e)
        {
            var text = textBoxMessage.Text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                MessageBus.Instance.Publish(new CustomDebugEvent(text));
                textBoxMessage.Clear();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
