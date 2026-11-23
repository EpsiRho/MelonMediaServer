using Microsoft.AspNetCore.Mvc.Filters;

namespace MelonWebApi.Middleware
{
    /// <summary>
    /// Keeps <c>count</c> and <c>page</c> on every list endpoint within bounds, so one request can't ask for the whole
    /// library at once or overflow <c>page * count</c>. Values are clamped rather than refused, so existing clients keep
    /// working (Galia's stats sync asks for 2,000 plays a page).
    /// </summary>
    public sealed class PagingLimits : IActionFilter
    {
        public const int MaxCount = 5000;

        public void OnActionExecuting(ActionExecutingContext context)
        {
            var args = context.ActionArguments;
            int count = MaxCount;
            if (args.TryGetValue("count", out var c) && c is int requested)
            {
                count = Math.Clamp(requested, 0, MaxCount);
                args["count"] = count;
            }
            if (args.TryGetValue("page", out var p) && p is int page)
            {
                args["page"] = Math.Clamp(page, 0, int.MaxValue / Math.Max(count, 1) - 1);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
