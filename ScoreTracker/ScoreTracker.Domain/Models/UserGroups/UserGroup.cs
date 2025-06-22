using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.UserGroups;

public class UserGroup
{
    public UserGroup(Name name)
    {
        Name = name;
    }

    public virtual Name Name { get; }
}