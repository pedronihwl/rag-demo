namespace Shared.Collections;

public class ContextCollection() : Collection("ctx")
{
    public DateOnly CreatedAt { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public string[] Files { get; set; } = [];
}