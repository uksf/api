using System.Text.Json.Serialization;

namespace UKSF.Api.ArmaServer.Models.Persistence;

/// <summary>
/// ACE medical state from ace_medical_fnc_serializeState.
/// Known fields are typed for querying; unknown fields captured in AdditionalData
/// for forward compatibility with ACE updates.
/// </summary>
public class AceMedicalState
{
    [JsonPropertyName("ace_medical_bloodVolume")]
    public double BloodVolume { get; set; }

    [JsonPropertyName("ace_medical_heartRate")]
    public double HeartRate { get; set; }

    [JsonPropertyName("ace_medical_bloodPressure")]
    public double[] BloodPressure { get; set; } = [];

    [JsonPropertyName("ace_medical_peripheralResistance")]
    public double PeripheralResistance { get; set; }

    [JsonPropertyName("ace_medical_hemorrhage")]
    public double Hemorrhage { get; set; }

    [JsonPropertyName("ace_medical_pain")]
    public double Pain { get; set; }

    [JsonPropertyName("ace_medical_inPain")]
    public bool InPain { get; set; }

    [JsonPropertyName("ace_medical_painSuppress")]
    public double PainSuppress { get; set; }

    [JsonPropertyName("ace_medical_openWounds")]
    public Dictionary<string, List<WoundEntry>> OpenWounds { get; set; } = new();

    [JsonPropertyName("ace_medical_bandagedWounds")]
    public Dictionary<string, List<WoundEntry>> BandagedWounds { get; set; } = new();

    [JsonPropertyName("ace_medical_stitchedWounds")]
    public Dictionary<string, List<WoundEntry>> StitchedWounds { get; set; } = new();

    [JsonPropertyName("ace_medical_fractures")]
    public double[] Fractures { get; set; } = [];

    [JsonPropertyName("ace_medical_tourniquets")]
    public double[] Tourniquets { get; set; } = [];

    [JsonPropertyName("ace_medical_bodyPartDamage")]
    public double[] BodyPartDamage { get; set; } = [];

    [JsonPropertyName("ace_medical_medications")]
    public List<MedicationEntry> Medications { get; set; } = [];

    [JsonPropertyName("ace_medical_occludedMedications")]
    public List<OccludedMedicationEntry> OccludedMedications { get; set; } = [];

    [JsonPropertyName("ace_medical_ivBags")]
    public List<IvBagEntry> IvBags { get; set; } = [];

    [JsonPropertyName("ace_medical_triageLevel")]
    public double TriageLevel { get; set; }

    [JsonPropertyName("ace_medical_triageCard")]
    public List<TriageCardEntry> TriageCard { get; set; } = [];

    [JsonPropertyName("ace_medical_statemachineState")]
    public string StateMachineState { get; set; } = string.Empty;

    [JsonPropertyName("ace_medical_logs")]
    public List<MedicalLogCategory> Logs { get; set; } = [];

    /// <summary>
    /// Captures any ACE medical fields not explicitly modelled above.
    /// Provides forward compatibility — new fields ACE adds will land here
    /// rather than causing deserialization failures.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}
