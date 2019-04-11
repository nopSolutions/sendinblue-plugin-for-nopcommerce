namespace Nop.Plugin.Misc.SendinBlue.Models
{
    /// <summary>
    /// Represents SMS model
    /// </summary>
    public class SmsModel
    {
        #region Properties

        public int Id { get; set; }

        public int MessageId { get; set; }

        public string Name { get; set; }

        public int PhoneTypeId { get; set; }

        public string PhoneType { get; set; }

        public string Text { get; set; }

        #endregion
    }
}