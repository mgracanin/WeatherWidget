# WeatherWidget development instructions

## Scope

Standalone Windows 11 widget. Read tasks/plan.md and tasks/todo.md before implementation. Preserve the source image assets and their ownership. Do not edit the original XWidget installation. Planning does not authorize starting implementation before the owner reviews the plan.

## Balanced model use (owner requested)

- Use the lowest-cost capable model for a clearly bounded task. Do not use a frontier model for every file or every review.
- Routine documentation, mechanical edits, straightforward XAML, fixtures and narrow bug fixes: prefer a lower-cost agent such as gpt-6-luna when available.
- Feature integration, asynchronous fetching, settings and Windows interop: prefer a standard workhorse such as gpt-6.1-sol. Give a lower-cost agent suitable isolated pieces where practical.
- Reserve frontier/high-effort reasoning for a demonstrated hard problem: unresolved Windows shell behavior, subtle concurrency/lifecycle bugs or a consequential architecture decision. Escalate with a short explanation of what failed and what needs resolving.
- Available model names and prices can change. The examples above are routing preferences, not pricing guarantees. Use the current supported equivalents if needed.
- The current chat's model cannot necessarily be changed by the agent. Do not claim to have switched it. Delegate a bounded implementation task to the appropriate available model when that reduces total effort.
- The owner authorizes selective subagent delegation for this balanced workflow. Prefer one agent per bounded task, sequentially. Parallel agents are justified only for independent work with an integration benefit.
- Do not delegate trivial actions, run multiple agents on the same task, or require a separate expensive review for every change. Keep prompts focused on the relevant files and acceptance criteria.
- Reassess after a lower-cost attempt fails; do not repeatedly retry an unsuitable model or immediately jump to the highest tier.
- Use local compilation, focused tests and actual Windows UI inspection as evidence. Mock-only checks do not establish desktop integration success.
- Batch independent reads, avoid repeated unchanged full-suite runs, and avoid unnecessary live weather requests. No autonomous paid API/model calls, subscriptions or hosted services without owner authorization.
- No LLM calls belong in the widget's runtime. Weather comes directly from the weather provider.

## Delivery

Implement one vertical slice at a time with focused validation and small commits. Report what was verified and any remaining limitation. Use the private origin repository. Keep tools/, build outputs, credentials and local settings out of Git.
