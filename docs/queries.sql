-- Illustrative SQL against the EF table names. Run against a disposable demo DB.
-- State values: 0 = New, 1 = InProgress, 2 = Done.
SELECT w."Id", w."Title", t."Name" AS "Assignee"
FROM "WorkItems" w
LEFT JOIN "TeamMembers" t ON t."Id" = w."AssigneeId"
WHERE w."Status" <> 2 AND w."DueDate" < CURRENT_DATE
ORDER BY w."DueDate", w."Id";

SELECT t."Name", COUNT(w."Id") AS "OpenItems"
FROM "TeamMembers" t
LEFT JOIN "WorkItems" w ON w."AssigneeId" = t."Id" AND w."Status" <> 2
GROUP BY t."Id", t."Name"
ORDER BY "OpenItems" DESC;
