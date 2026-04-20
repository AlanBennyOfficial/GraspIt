using FluentAssertions;
using GraspIt.Core.Entities;
using System;
using System.Collections.Generic;
using Xunit;

namespace GraspIt.Tests.Core.Entities;

public class ChatEntitiesTests
{
    [Fact]
    public void ChatFolder_Initialization_SetsDefaultValues()
    {
        var folder = new ChatFolder();

        folder.Id.Should().Be(0);
        folder.Name.Should().Be(string.Empty);
        folder.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        folder.Sessions.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ChatFolder_Properties_CanBeSet()
    {
        var dt = new DateTime(2025, 1, 1);
        var user = new User { Id = 1 };
        var folder = new ChatFolder
        {
            Id = 5,
            Name = "My Folder",
            CreatedAt = dt,
            UserId = 1,
            User = user,
            Sessions = new List<ChatSession>()
        };

        folder.Id.Should().Be(5);
        folder.Name.Should().Be("My Folder");
        folder.CreatedAt.Should().Be(dt);
        folder.UserId.Should().Be(1);
        folder.User.Should().Be(user);
        folder.Sessions.Should().NotBeNull();
    }

    [Fact]
    public void ChatMessage_Initialization_SetsDefaultValues()
    {
        var msg = new ChatMessage();

        msg.Id.Should().Be(0);
        msg.Role.Should().Be("user");
        msg.Content.Should().Be(string.Empty);
        msg.UsedRag.Should().BeFalse();
        msg.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ChatMessage_Properties_CanBeSet()
    {
        var dt = new DateTime(2025, 1, 1);
        var session = new ChatSession { Id = 2 };
        var msg = new ChatMessage
        {
            Id = 10,
            Role = "assistant",
            Content = "Hello",
            UsedRag = true,
            CreatedAt = dt,
            SessionId = 2,
            Session = session
        };

        msg.Id.Should().Be(10);
        msg.Role.Should().Be("assistant");
        msg.Content.Should().Be("Hello");
        msg.UsedRag.Should().BeTrue();
        msg.CreatedAt.Should().Be(dt);
        msg.SessionId.Should().Be(2);
        msg.Session.Should().Be(session);
    }

    [Fact]
    public void ChatSession_Initialization_SetsDefaultValues()
    {
        var session = new ChatSession();

        session.Id.Should().Be(0);
        session.Title.Should().Be("New Chat");
        session.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        session.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        session.Messages.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ChatSession_Properties_CanBeSet()
    {
        var dt = new DateTime(2025, 1, 1);
        var user = new User { Id = 1 };
        var folder = new ChatFolder { Id = 3 };
        var session = new ChatSession
        {
            Id = 20,
            Title = "My Session",
            CreatedAt = dt,
            UpdatedAt = dt,
            UserId = 1,
            User = user,
            FolderId = 3,
            Folder = folder,
            Messages = new List<ChatMessage>()
        };

        session.Id.Should().Be(20);
        session.Title.Should().Be("My Session");
        session.CreatedAt.Should().Be(dt);
        session.UpdatedAt.Should().Be(dt);
        session.UserId.Should().Be(1);
        session.User.Should().Be(user);
        session.FolderId.Should().Be(3);
        session.Folder.Should().Be(folder);
        session.Messages.Should().NotBeNull();
    }
}
