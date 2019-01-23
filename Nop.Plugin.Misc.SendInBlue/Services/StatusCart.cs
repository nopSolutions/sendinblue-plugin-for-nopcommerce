namespace Nop.Plugin.Misc.SendInBlue.Services
{
    /// <summary>
    /// Represents staus shopping cart
    /// </summary>
    public enum StatusCart
    {
        /// <summary>
        /// When a cart is created
        /// </summary>
        Created = 0,

        /// <summary>
        /// When an item is added to an existing cart
        /// </summary>
        Updated = 1,

        /// <summary>
        /// When a cart is emptied
        /// </summary>
        Deleted = 2
    }
}