using System;

namespace Workshop.Sample;

public sealed class CreatePostHandler
{
    public void Handle(CreatePostCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        throw new NotImplementedException("Educational skeleton: implement a reviewed use case before use.");
    }
}
