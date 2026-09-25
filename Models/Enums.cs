namespace StartupConnect.Models;

public enum TimeAvailability
{
    FullTime,
    PartTime,
    WeekendsOnly
}

public enum InvestmentCapacity
{
    None,
    UpTo10K,
    From10KTo50K,
    From50KTo1L,
    Above1L
}

public enum IdeaStatus
{
    Draft,
    Submitted,
    UnderReview,
    Approved,
    Rejected
}

public enum InterestType
{
    Work,
    Invest,
    Both
}

public enum InterestStatus
{
    Pending,
    Accepted,
    Rejected,
    Cancelled
}

public enum TeamStatus
{
    Forming,
    Active,
    Launched,
    Closed
}

public enum IdeaProgressStage
{
    Idea,
    Research,
    Prototype,
    MVP,
    Testing,
    Launch
}
