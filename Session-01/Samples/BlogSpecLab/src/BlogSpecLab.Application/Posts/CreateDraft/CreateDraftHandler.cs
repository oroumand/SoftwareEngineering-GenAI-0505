using BlogSpecLab.Application.Posts.Ports;
using BlogSpecLab.Domain.Posts.Entities;
using BlogSpecLab.Domain.Posts.Errors;
using BlogSpecLab.Domain.Posts.ValueObjects;

namespace BlogSpecLab.Application.Posts.CreateDraft;

public sealed class CreateDraftHandler(IPostRepository repository)
{
    public async Task<CreateDraftResult> HandleAsync(CreateDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            var post = Post.CreateDraft(
                PostId.New(),
                new AuthorId(command.AuthorId),
                new PostTitle(command.Title),
                new PostContent(command.Content));
            var version = await repository.AddAsync(post, cancellationToken);
            return new CreateDraftResult(post.Id, version);
        }
        catch (PostValidationException exception)
        {
            throw new ValidationUseCaseException(exception.Message);
        }
    }
}
