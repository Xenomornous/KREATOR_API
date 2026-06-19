namespace Kreator_API.Models;

public class SidebarModule
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    public List<SidebarLesson> Lessons { get; set; }
        = [];
}

public class SidebarLesson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    public List<SidebarLecture> Lectures { get; set; }
        = [];
}

public class SidebarLecture
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}