# Repository working agreements

- Make all repository changes on a separate branch and submit a pull request. The project owner reviews and merges into main. Never push directly to main or merge PRs on their behalf.
- This is a focused class/portfolio warehouse inventory project. Follow docs/project-plan.md, docs/requirements.md, and the two milestones in docs/roadmap.md; do not add unrelated features.
- Each cloud task already has an isolated checkout. Use it; do not create a worktree unless the user requests one.
- Keep SQL Server credentials, connection strings, OMDb keys, and real personal data outside source control. Use fake demonstration data.
- Never run schema changes, seeding, or automated create/drop tests against the teacher's shared database without explicit authorization and the correct connection context.
- Follow global.json. Restore with locked dependencies and run dotnet test BlueBox.sln. Distinguish SQLite relational results, SQL Server results, and Windows/teacher-server checks.
- Manager administration, corrections, retirement workflows, OMDb, and the overview dashboard are Milestone 2; do not claim they are delivered by Milestone 1.
