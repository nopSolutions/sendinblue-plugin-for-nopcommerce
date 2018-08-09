namespace Nop.Plugin.Misc.SendInBlue.Models
{
    public class ListSMSModel
    {
        public int Id { get; set; }

        public int MessageId { get; set; }

        public string Name { get; set; }

        public bool SMSActive { get; set; }

        public int PhoneTypeId { get; set; }

        public string PhoneType { get; set; }

        public string Text { get; set; }
    }
}