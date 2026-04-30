namespace Kreator_API.Models
{
    // 1. Klasa reprezentująca wnętrze JSONa
    public class LessonData
    {
        public string Content { get; set; }
        public int Difficulty { get; set; }
        
    }

    // 2. Twój główny model
    public class Lesson_structure
    {
        public int Id { get; set; }
        public string User_Id { get; set; }
        public string Module_Id { get; set; }
        public string Module_Name { get; set; }
        public string Lesson_Id { get; set; }
        public string Lesson_Name { get; set; }
        public string Role { get; set; }
        public string Mod_User { get; set; }
        public DateTime Mod_Date { get; set; }

        // TUTAJ: używasz klasy LessonData
        public LessonData Json_Structure { get; set; }
    }
}