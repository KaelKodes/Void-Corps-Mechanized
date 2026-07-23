namespace Mechanize;

/// <summary>What to launch after the player locks Cat/Dog on a fresh profile.</summary>
public enum PendingFactionContinue
{
	None = 0,
	SolarTutorial,
	SolarSkipConvention,
	RoguelikeCadet,
	RoguelikeConvention,
	/// <summary>Return to main menu profile hub after create wizard.</summary>
	NewProfile
}
