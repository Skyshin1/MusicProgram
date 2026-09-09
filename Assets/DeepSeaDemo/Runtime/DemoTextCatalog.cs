using System;
using System.Collections.Generic;
using UnityEngine;
namespace DeepSeaDemo
{
    [CreateAssetMenu(menuName = "Deep Sea Demo/English Text Catalog")]
    public sealed class DemoTextCatalog : ScriptableObject
    {
        [Serializable] public struct Entry { public string id; [TextArea(2, 14)] public string text; }
        public Entry[] entries = Defaults();
        Dictionary<string, string> lookup;
        void OnEnable() => lookup = null;
        void OnValidate() => lookup = null;
        public string Resolve(string id)
        {
            if (lookup == null) { lookup = new(); foreach (var e in entries) if (!string.IsNullOrEmpty(e.id)) lookup[e.id] = e.text; }
            if (lookup.TryGetValue(id, out var text)) return text;
            // Existing serialized catalogs may predate newly added feedback entries.
            foreach (var entry in defaults) if (entry.id == id) return entry.text;
            return id;
        }
        public static string Get(string id)
        {
            var catalog = DemoFlow.Instance != null ? DemoFlow.Instance.config.text : null;
            if (catalog != null) return catalog.Resolve(id);
            foreach (var e in defaults) if (e.id == id) return e.text;
            return id;
        }
        static readonly Entry[] defaults = Defaults();
        public static Entry[] Defaults() => new Entry[] {
            new Entry { id = "sonar.sent", text = "SONAR PULSE SENT - watch nearby surfaces for white outlines." },
            new Entry { id = "sonar.surface", text = "Sonar works underwater only. Submerge your head first." },
            new Entry { id = "sonar.cooldown", text = "Sonar recharging. Release Trigger and press again shortly." },
            new Entry { id = "sonar.holding", text = "This hand is holding an item. Use the empty hand's Trigger for sonar." },
            new Entry { id = "sonar.paused", text = "Sonar unavailable while the game is paused or in a menu." },
            new Entry { id = "sonar.ui", text = "Trigger is being used by the menu or reading panel." },
            new Entry { id = "sonar.interaction", text = "Trigger is interacting with the pointed-at object." },
            new Entry { id = "sonar.qte", text = "Finish the repair skill check before using sonar." },
            new Entry { id = "log01.title", text = "Routine Voyage Log" },
            new Entry { id = "log01.body", text = "12 June. The supply boat arrived half an hour early. Fresh vegetables at last. We checked the southern ladder and the winch; the sea was quieter than the training basin. Our new diver joked that this would be an easy posting.\n\nAfter lunch, the sonar operator found a repeating echo twenty-four metres below the platform. It sounded like someone tapping a steel pipe. Nothing was loose in the pump room. He marked it as a seabed reflection and promised another survey tomorrow. At dinner we called it our neighbour asking us to stop working.\n\nThe echo returned after lights-out. There was no alarm. I wrote the time in the paper log because the terminal had already shut down: 23:17. Beyond the windows, the sea looked perfectly still." },
            new Entry { id = "log02.title", text = "Night Shift: Equipment Faults" },
            new Entry { id = "log02.body", text = "14 June. Array channel three failed at 23:17. The discharge pump tripped immediately afterwards. Both times are in the signed paper log. The exported alarm report says 00:03 the next morning: scheduled maintenance, no personnel risk. Nobody on this shift signed a maintenance order.\n\nThe technician found dents pressed inward through the housing. Corrosion cannot explain them. When we shut the equipment down, the knocks stopped. When we tested the array, the echoes came closer. Nobody laughed this time.\n\nThe supervisor says speculation is unprofessional. He wants the original records uploaded, and the paper copy corrected to match the system. I have kept this page unchanged. If these times are a clerical error, why does the report need to be replaced before anyone inspects the damage?" },
            new Entry { id = "log03.title", text = "Diver Observation" },
            new Entry { id = "log03.body", text = "16 June. Visibility near the outfall is collapsing. My flashlight showed a few metres of drifting particles and nothing beyond them. An empty beam does not mean empty water.\n\nMy partner triggered active sonar. Something moved behind the pump frame. On the next pulse it was closer. We switched off the equipment and sheltered behind the rocks. It passed us and followed the pipe that was still vibrating. The metal groaned. For the first time, I could not tell myself it was just the current.\n\nWe returned holding each other's wrists. Do not scan continuously. Do not follow an echo into open water. A stone thrown against a distant metal frame drew its attention long enough for us to leave. Take cover first, make one decision, and then move. Sound travels much farther than our lights." },
            new Entry { id = "log04.title", text = "The Unsent Report" },
            new Entry { id = "log04.body", text = "18 June. The company requested that we remove the contamination peak and submit last week's water sample as this week's result. I cannot sign that report.\n\nThe submarine recorder still holds the original sensor timestamps. Discharge began before the alarm. The final images show the creatures striking the transmitting sonar array, then the discharge pump. They did not pursue the divers who had switched off their equipment and withdrawn. We described our fear as something evil in the ocean. We did not describe what we were pumping into its home.\n\nIf you find this page, bring back the black box. Collect a fresh sample if it is safe. Compare the sensor log with the handwritten times. Do not rely on the company's remote summary. Listen to the complete recording before deciding where the evidence should go." },
            new Entry { id = "runtime.000", text = "Read the Routine Voyage Log in the safe room." },
            new Entry { id = "runtime.001", text = "Compare the alarm timestamps with the paper log." },
            new Entry { id = "runtime.002", text = "Pick up the flashlight from the equipment table." },
            new Entry { id = "runtime.003", text = "Confirm your diving suit at the equipment station." },
            new Entry { id = "runtime.004", text = "Enter the water. Right stick down: dive. Empty-hand Trigger: sonar." },
            new Entry { id = "runtime.005", text = "Follow the pipes to the repair cage. Find the electronic lock tool. Outfall evidence is optional." },
            new Entry { id = "runtime.006", text = "Enter the flooded compartment. Read the last recording and retrieve the orange black box." },
            new Entry { id = "runtime.007", text = "Repair the submarine hatch: hold tool Trigger; judge the QTE with the other empty hand's Grip." },
            new Entry { id = "runtime.008", text = "Bring the black box back to the platform. Follow the blue return beacons." },
            new Entry { id = "runtime.009", text = "Insert the black box into the analysis socket. Review the evidence, then preserve or upload it." },
            new Entry { id = "runtime.010", text = "Investigation complete." },
            new Entry { id = "runtime.011", text = "Contaminated water. The flashlight reveals nearby surfaces. Sonar reveals outlines, but also reveals you." },
            new Entry { id = "runtime.012", text = "Checkpoint unavailable. Please start a new game." },
            new Entry { id = "runtime.013", text = "Equipment ready. Weather is deteriorating. Follow the yellow signs to the dive point." },
            new Entry { id = "runtime.014", text = "Hatch unlocked. The compartment is flooded. Watch your oxygen." },
            new Entry { id = "runtime.015", text = "Company Data Reception" },
            new Entry { id = "runtime.016", text = "Uploading mission records...\nThis is a fictional sequence. No real files are uploaded." },
            new Entry { id = "runtime.017", text = "Automatic Cleanup Protocol" },
            new Entry { id = "runtime.018", text = "Black-box copy: removed\nLocal sensor index: removed\nCompany conclusion: equipment failure. No abnormal discharge." },
            new Entry { id = "runtime.019", text = "Last Recording" },
            new Entry { id = "runtime.020", text = "Backup power restored. The recording shows the creature striking the active sonar array, then the discharge pump.\nIt did not follow the divers who switched off their equipment and withdrew.\n\nThe engine is making noise. Take the black box and leave." },
            new Entry { id = "runtime.021", text = "Backup power is online. Take the black box and follow the return beacons." },
            new Entry { id = "runtime.022", text = "The checkpoint could not be written to disk. Retry is still available for this session." },
            new Entry { id = "runtime.023", text = "Destination obstructed. Movement cancelled." },
            new Entry { id = "runtime.024", text = " was moved to its recovery point." },
            new Entry { id = "runtime.025", text = "DEEP SEA / THE UNSENT REPORT" },
            new Entry { id = "runtime.026", text = "New Game" },
            new Entry { id = "runtime.027", text = "Continue Checkpoint" },
            new Entry { id = "runtime.028", text = "Settings" },
            new Entry { id = "runtime.029", text = "Quit" },
            new Entry { id = "runtime.030", text = "Quest 3 / Link or Air Link\nGrip: grab / Trigger: use or select" },
            new Entry { id = "runtime.031", text = "Investigation Paused" },
            new Entry { id = "runtime.032", text = "Resume" },
            new Entry { id = "runtime.033", text = "Return to Safe Position" },
            new Entry { id = "runtime.034", text = "Retry Chapter Checkpoint" },
            new Entry { id = "runtime.035", text = "Main Menu" },
            new Entry { id = "runtime.036", text = "Comfort & Audio" },
            new Entry { id = "runtime.037", text = "Volume -" },
            new Entry { id = "runtime.038", text = "Volume +" },
            new Entry { id = "runtime.039", text = "Turn: Snap / Smooth" },
            new Entry { id = "runtime.040", text = "Move Speed: Slow / Standard" },
            new Entry { id = "runtime.041", text = "Lightning: Standard / Gentle" },
            new Entry { id = "runtime.042", text = "Story Subtitles: " },
            new Entry { id = "runtime.043", text = "On" },
            new Entry { id = "runtime.044", text = "Off" },
            new Entry { id = "runtime.045", text = " (documents remain available)" },
            new Entry { id = "runtime.046", text = "Back" },
            new Entry { id = "runtime.047", text = "FIELD ARCHIVE / READ AGAIN ANYTIME" },
            new Entry { id = "runtime.048", text = "Close Record" },
            new Entry { id = "runtime.049", text = "Black Box Decoded" },
            new Entry { id = "runtime.050", text = "EVIDENCE CROSS-CHECK" },
            new Entry { id = "runtime.051", text = "Black box: decoded\n" },
            new Entry { id = "runtime.052", text = "Sensor: discharge preceded the alarm\n" },
            new Entry { id = "runtime.053", text = "Sensor log: not recovered\n" },
            new Entry { id = "runtime.054", text = "Water sample: abnormal contamination\n" },
            new Entry { id = "runtime.055", text = "Water sample: not submitted\n" },
            new Entry { id = "runtime.056", text = "Testimony: impacts stopped when sonar stopped\n" },
            new Entry { id = "runtime.057", text = "Survivor testimony: not read\n" },
            new Entry { id = "runtime.058", text = "\nThe creatures targeted sonar arrays and discharge pumps first. The company requests an immediate upload. Its protocol includes deleting the local copies." },
            new Entry { id = "runtime.059", text = "Preserve Evidence / Refuse Upload" },
            new Entry { id = "runtime.060", text = "Upload to Company..." },
            new Entry { id = "runtime.061", text = "Ending: Case Closed" },
            new Entry { id = "runtime.062", text = "Ending: The Unsent Report" },
            new Entry { id = "runtime.063", text = "You followed the company's instructions.\nThe anomalies were classified as equipment failure.\nThe platform resumed work. The echoes below did not stop." },
            new Entry { id = "runtime.064", text = "You refused to upload.\nThe evidence still connects the pollution to the attacks.\nThe next person who enters these waters will not have to trust the altered report." },
            new Entry { id = "runtime.065", text = "\n\nThank you for playing / Deep Sea Investigation" },
            new Entry { id = "runtime.066", text = "Start Again" },
            new Entry { id = "runtime.067", text = "Return to Main Menu" },
            new Entry { id = "runtime.068", text = "Confirm Upload?" },
            new Entry { id = "runtime.069", text = "The company protocol will erase the fictional in-game records.\nThis choice determines the ending." },
            new Entry { id = "runtime.070", text = "Confirm Upload" },
            new Entry { id = "runtime.071", text = "Back to Evidence" },
            new Entry { id = "runtime.072", text = "Volume " },
            new Entry { id = "runtime.073", text = "Turning mode changed." },
            new Entry { id = "runtime.074", text = "Movement speed changed." },
            new Entry { id = "runtime.075", text = "Lightning setting changed." },
            new Entry { id = "runtime.076", text = "\nOxygen " },
            new Entry { id = "runtime.077", text = "%    Acoustic Exposure " },
            new Entry { id = "runtime.078", text = "Pick up the flashlight from the equipment table first." },
            new Entry { id = "runtime.079", text = "Diving suit ready. Your oxygen gauge is on your glove." },
            new Entry { id = "runtime.080", text = "Place the matching item in the receiver." },
            new Entry { id = "runtime.081", text = "Release the black box into the socket. The evidence panel opens when playback finishes." },
            new Entry { id = "runtime.082", text = "Evidence registered: " },
        };
    }
}
