using System;
using System.IO;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private CommanderTurnObservationStore localObservationStore;
        private Button localObservationToggle;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CommanderObservation_Sync();
        [DllImport("__Internal")] private static extern void CommanderObservation_Download(string json);
#endif

        private CommanderTurnObservationStore LocalObservationStore
        {
            get
            {
                if (localObservationStore != null) return localObservationStore;
                string file = Path.Combine(Application.persistentDataPath,
                    "CommanderEvidence", "commander-observations.json");
                localObservationStore = new CommanderTurnObservationStore(file);
#if UNITY_WEBGL && !UNITY_EDITOR
                localObservationStore.SetPersistedCallback(CommanderObservation_Sync);
#endif
                return localObservationStore;
            }
        }

        private void ToggleLocalObservationRecording()
        {
            LocalObservationStore.SetRecording(!LocalObservationStore.Recording);
            UpdateLocalObservationToggle();
            AppendLine("Commander", LocalObservationStore.Recording
                ? "Local metrics recording is on for this session. No metrics are uploaded."
                : "Local metrics recording is off. Existing local records remain available for export.", false);
        }

        private void UpdateLocalObservationToggle()
        {
            if (localObservationToggle == null) return;
            TMP_Text label = localObservationToggle.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = "Record local metrics: "
                + (LocalObservationStore.Recording ? "On" : "Off");
        }

        private void ExportLocalObservations()
        {
            string json = LocalObservationStore.ExportJson();
            if (System.Text.Encoding.UTF8.GetByteCount(json) > CommanderTurnObservationStore.MaximumExportBytes)
            {
                AppendLine("Commander", "Local metrics export exceeded its bound; nothing was exported.", false);
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                CommanderObservation_Download(json);
                AppendLine("Commander", "Browser download requested for local metrics. No metrics were uploaded.", false);
            }
            catch (Exception)
            {
                AppendLine("Commander", "Browser download could not be started.", false);
            }
#else
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "CommanderEvidence");
                Directory.CreateDirectory(directory);
                string destination = Path.Combine(directory, "commander-metrics-export-"
                    + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
                string temporary = destination + ".tmp";
                File.WriteAllText(temporary, json);
                File.Move(temporary, destination);
                AppendLine("Commander", "Local metrics exported to " + destination
                    + ". No metrics were uploaded.", false);
            }
            catch (Exception)
            {
                AppendLine("Commander", "Local metrics export failed; existing records were retained.", false);
            }
#endif
        }
    }
}
