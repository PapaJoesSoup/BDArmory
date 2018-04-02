namespace BDArmory.UI
{
    /// <summary>
    /// Implement this interface for your module to appear in the weapon manager modules list
    /// </summary>
    public interface IBDWMModule
    {
        // module name
        string Name { get; }
        // is the module enabled
        bool Enabled { get; }
        // toggle the module
        void Toggle();
    }
}
