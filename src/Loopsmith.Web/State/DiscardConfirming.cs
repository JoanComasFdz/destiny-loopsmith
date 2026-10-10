using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Web.Hosting;

namespace Loopsmith.Web.State;

/// <summary>Impure: asks before an action that replaces the open loop (nothing is stored in the browser, so it would be lost).</summary>
public static class DiscardConfirming
{
    /// <summary>True when there is nothing to lose (no loop open, or one without steps) or the user agrees to lose it.</summary>
    public static async Task<bool> ConfirmDiscardAsync(Workbench workbench, BrowserInterop browser, string question)
    {
        if (workbench.Session is not Optional<DesignSession>.Some { Value.Design.Steps.Length: > 0 } open)
        {
            return true;
        }

        var steps = open.Value.Design.Steps.Length;
        var confirmed = await browser.ConfirmAsync(                                              // impure
            $"{question} The current loop's {steps} step{(steps == 1 ? "" : "s")} will be discarded — export it or copy its share link first to keep it.");
        return confirmed;
    }
}
