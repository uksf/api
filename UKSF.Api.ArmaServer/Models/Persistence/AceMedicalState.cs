using System.Text.Json.Serialization;

namespace UKSF.Api.ArmaServer.Models.Persistence;

/// <summary>
/// ACE medical state from ace_medical_fnc_serializeState.
/// Known fields are typed for querying; unknown fields captured in AdditionalData
/// for forward compatibility with ACE updates.
/// </summary>
public class AceMedicalState
{
    // SQF identifiers are case-insensitive; BIS str() of a hashmap lowercases all keys.
    // ACE medical state arrives from-game with lowercase compound names — match exactly
    // so the load-path payload (built via JSON-serialise → re-deserialise as Dict) emits
    // keys identical to the profile-side snapshot. Avoids spurious case mismatches in
    // proofing comparison.
    [JsonPropertyName("ace_medical_bloodvolume")]
    public double BloodVolume { get; set; }

    [JsonPropertyName("ace_medical_heartrate")]
    public double HeartRate { get; set; }

    [JsonPropertyName("ace_medical_bloodpressure")]
    public double[] BloodPressure { get; set; } = [];

    [JsonPropertyName("ace_medical_peripheralresistance")]
    public double PeripheralResistance { get; set; }

    [JsonPropertyName("ace_medical_hemorrhage")]
    public double Hemorrhage { get; set; }

    [JsonPropertyName("ace_medical_pain")]
    public double Pain { get; set; }

    [JsonPropertyName("ace_medical_inpain")]
    public bool InPain { get; set; }

    [JsonPropertyName("ace_medical_painsuppress")]
    public double PainSuppress { get; set; }

    [JsonPropertyName("ace_medical_openwounds")]
    public Dictionary<string, List<WoundEntry>> OpenWounds { get; set; } = new();

    [JsonPropertyName("ace_medical_bandagedwounds")]
    public Dictionary<string, List<WoundEntry>> BandagedWounds { get; set; } = new();

    [JsonPropertyName("ace_medical_stitchedwounds")]
    public Dictionary<string, List<WoundEntry>> StitchedWounds { get; set; } = new();

    [JsonPropertyName("ace_medical_fractures")]
    public double[] Fractures { get; set; } = [];

    [JsonPropertyName("ace_medical_tourniquets")]
    public double[] Tourniquets { get; set; } = [];

    [JsonPropertyName("ace_medical_bodypartdamage")]
    public double[] BodyPartDamage { get; set; } = [];

    [JsonPropertyName("ace_medical_medications")]
    public List<MedicationEntry> Medications { get; set; } = [];

    [JsonPropertyName("ace_medical_occludedmedications")]
    public List<OccludedMedicationEntry> OccludedMedications { get; set; } = [];

    [JsonPropertyName("ace_medical_ivbags")]
    public List<IvBagEntry> IvBags { get; set; } = [];

    [JsonPropertyName("ace_medical_triagelevel")]
    public double TriageLevel { get; set; }

    [JsonPropertyName("ace_medical_triagecard")]
    public List<TriageCardEntry> TriageCard { get; set; } = [];

    [JsonPropertyName("ace_medical_statemachinestate")]
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
