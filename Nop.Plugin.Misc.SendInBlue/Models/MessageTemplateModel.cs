namespace Nop.Plugin.Misc.SendinBlue.Models
{
    /// <summary>
    /// Represents message template model
    /// </summary>
    public class MessageTemplateModel
    {
        #region Properties

        public int Id { get; set; }

        public string Name { get; set; }

        public bool IsActive { get; set; }

        public string ListOfStores { get; set; }

        public string TemplateType { get; set; }

        public int TemplateTypeId { get; set; }

        public string EditLink { get; set; }

        #endregion
    }
}