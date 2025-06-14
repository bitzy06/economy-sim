# Government Features Implementation

This update introduces a basic government system with political parties, policies and departments.

## Key Elements
- **Government class** with lists of parties, policies and departments.
- **PoliticalParty** records party name and share of power.
- **Policy** objects store policy name, type and status. Types include `Economic`, `Financial` and `Political`.
- **Department** class defines government departments (state bank is created by default).

## User Interface
- Added a **Government** tab to the main game window. It lists political parties and their share of government.
- A **Policy Manager** button opens a new window displaying existing policies and departments.

## Notes
The Department class currently only stores a name and budget allocation; behaviour for AI or player control is left as a future task.
