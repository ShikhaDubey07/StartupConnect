
namespace StartupConnect.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string IconClass { get; set; } = "bi-lightbulb";

    public ICollection<Idea> Ideas { get; set; } = new List<Idea>();
}

public class UserInterestTag
{
    public int Id { get; set; }
    public int UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = null!;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}

public class UserSkill
{
    public int Id { get; set; }
    public int UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = null!;
    public string SkillName { get; set; } = string.Empty;
}
