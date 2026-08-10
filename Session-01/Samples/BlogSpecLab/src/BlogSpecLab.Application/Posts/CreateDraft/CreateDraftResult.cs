using BlogSpecLab.Application.Posts.Ports;
using BlogSpecLab.Domain.Posts.ValueObjects;

namespace BlogSpecLab.Application.Posts.CreateDraft;

public sealed record CreateDraftResult(PostId Id, PostVersion Version);
