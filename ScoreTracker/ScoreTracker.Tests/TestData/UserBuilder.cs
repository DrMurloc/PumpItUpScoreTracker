using System;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Tests.TestData;

internal sealed class UserBuilder
{
    private Guid _id = Guid.NewGuid();
    private Name _name = Name.From("test-user");
    private bool _isPublic = true;
    private Name? _gameTag;
    private Uri _profileImage = new("https://example.invalid/avatar.png");
    private Name? _country;

    public UserBuilder WithId(Guid id) { _id = id; return this; }
    public UserBuilder WithName(string name) { _name = Name.From(name); return this; }
    public UserBuilder WithName(Name name) { _name = name; return this; }
    public UserBuilder WithIsPublic(bool isPublic) { _isPublic = isPublic; return this; }
    public UserBuilder WithGameTag(string gameTag) { _gameTag = Name.From(gameTag); return this; }
    public UserBuilder WithProfileImage(Uri profileImage) { _profileImage = profileImage; return this; }
    public UserBuilder WithCountry(string country) { _country = Name.From(country); return this; }

    public User Build() => new(_id, _name, _isPublic, _gameTag, _profileImage, _country);

    public static implicit operator User(UserBuilder b) => b.Build();
}
