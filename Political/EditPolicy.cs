namespace Economy_sim
{
    /// <summary>
    /// Edit policy for how to apply edits to the grid.
    /// Kept minimal now that the authoritative/LOD system is removed.
    /// </summary>
    public enum EditPolicy
    {
        FillAllSubcells,    // Safe: fill all coarse cells fully
        BorderAware         // Pretty: future hook for coastlines/boundary-aware edits
    }
}
