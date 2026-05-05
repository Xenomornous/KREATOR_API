namespace Kreator_API.Models
{
    public class Lecture
    {
        public int Id { get; set; }
        public string User_Id { get; set; }
        public string Module_Id { get; set; }
        public string Module_Name { get; set; }
        public string Lesson_Id { get; set; }
        public string Lesson_Name { get; set; }

        public string Lecture_Id { get; set; }
        public string Lecture_Name { get; set; }

        public string Role { get; set; }
        public string Mod_User { get; set; }
        public DateTime Mod_Date { get; set; }
    }
}