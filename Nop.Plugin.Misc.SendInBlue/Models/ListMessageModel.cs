namespace Nop.Plugin.Misc.SendInBlue.Models
{
    public class ListMessageModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public bool IsActive { get; set; }

        public string ListOfStores { get; set; }

        public string TemplateType { get; set; }

        public int TemplateTypeId { get; set; }

        public string EditLink { get; set; }
    }
}