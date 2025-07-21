using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace EconomySim
{
    public partial class EventViewer : Form
    {
        private class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public string Type { get; set; } = string.Empty;
            public string Data { get; set; } = string.Empty;
        }

        private readonly List<LogEntry> events = new();

        public EventViewer()
        {
            InitializeComponent();
            buttonOpen.Click += ButtonOpen_Click;
            scrubBar.Scroll += ScrubBar_Scroll;
        }

        private void ButtonOpen_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Event Logs (*.jsonl)|*.jsonl|All files|*.*",
                InitialDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs")
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                LoadLog(ofd.FileName);
            }
        }

        private void LoadLog(string path)
        {
            events.Clear();
            foreach (var line in File.ReadLines(path))
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<LogEntry>(line);
                    if (entry != null)
                        events.Add(entry);
                }
                catch
                {
                    // ignore parse errors
                }
            }

            eventsList.Items.Clear();
            foreach (var ev in events)
            {
                eventsList.Items.Add($"{ev.Timestamp:HH:mm:ss} {ev.Type}");
            }

            scrubBar.Minimum = 0;
            scrubBar.Maximum = events.Count > 0 ? events.Count - 1 : 0;
            scrubBar.Value = 0;
            if (eventsList.Items.Count > 0)
                eventsList.SelectedIndex = 0;
        }

        private void ScrubBar_Scroll(object? sender, EventArgs e)
        {
            int index = scrubBar.Value;
            if (index >= 0 && index < eventsList.Items.Count)
            {
                eventsList.SelectedIndex = index;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
