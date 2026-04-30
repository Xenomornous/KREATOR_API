namespace Kreator_API.Models_Module
{
    public class Module
    {
        public int Id { get; set; }

        public string User_Id { get; set; } = string.Empty;

        public string Module_Id { get; set; } = string.Empty;

        public string Module_Name { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string Mod_User { get; set; } = string.Empty;

        public DateTime Mod_Date { get; set; }
    }
}