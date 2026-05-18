using LayeredTemplate.App.Features.TodoLists.Models;

namespace LayeredTemplate.App.Features.TodoLists.Services;

/// <summary>
/// Computes a numeric "quality score" for a <see cref="TodoListDto"/>. Pure example of a
/// feature-internal service — referenced only by TodoLists endpoints. If the rating logic was
/// needed by another feature, the interface would move to <c>Shared/</c>; today it isn't, so
/// it stays here.
/// </summary>
public interface ITodoListRatingService
{
    decimal Rate(TodoListDto todoList);
}

internal sealed class TodoListRatingService : ITodoListRatingService
{
    public decimal Rate(TodoListDto todoList)
    {
        // Trivial scoring: name length, bonus for description, bonus for explicit Type.
        // Real implementations would consult historical engagement / business rules / etc.
        var score = (decimal)todoList.Name.Length;
        if (!string.IsNullOrWhiteSpace(todoList.Description))
        {
            score += 10;
        }

        if (todoList.Type != TodoListType.Type1)
        {
            score += 5;
        }

        return score;
    }
}
