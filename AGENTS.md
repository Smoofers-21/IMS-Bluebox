# Repository working agreements

- Make all repository changes on a separate branch and submit a pull request. The project owner reviews and merges into main. Never push directly to main or merge PRs on their behalf.
- This is a focused class/portfolio warehouse inventory project. Follow docs/project-plan.md, docs/requirements.md, and the two milestones in docs/roadmap.md; do not add unrelated features.
- Each cloud task already has an isolated checkout. Use it; do not create a worktree unless the user requests one.
- Keep SQL Server credentials, connection strings, OMDb keys, and real personal data outside source control. Use fake demonstration data.
- Never run schema changes, seeding, or automated create/drop tests against the teacher's shared database without explicit authorization and the correct connection context.
- Follow the instructor's Program.Main/MapRoutes and ADO.NET SqlConnection/SqlCommand/DataTable baseline. Do not reintroduce Entity Framework without the owner's approval. Razor Pages render the existing screens.
- Follow global.json. Run both web application and real SQL Server tests locally with `bash scripts/test-local.sh` (Docker and the pinned SDK required). It uses a disposable local SQL Server container, locked restore, and the full Release suite; never point automated create/drop tests at the Azure portfolio database. Report actual SQL execution separately from any skips. For web UI changes, also run browser checks for the affected flow.
- Manager administration, corrections, retirement workflows, OMDb, and the overview dashboard are Milestone 2; do not claim they are delivered by Milestone 1.
- Keep the README, project plan and Word copy, setup, roadmap, and validation record aligned with delivered behavior. Cite verified commits/runs; keep Windows/teacher-server checks, live OMDb checks, screenshots, and public deployment pending until completed.
- `BlueBox.sln` remains the checked-in/CI solution. Follow docs/submission.md to generate `.slnx` for hand-in; changing the repository solution format is a separate branch/PR change.
