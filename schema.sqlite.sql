-- SQLite schema for the public demo.
--
-- This is the same shape as schema.sql (MS-SQL) with the constraints the
-- original never declared: primary keys, NOT NULL, and a real foreign key from
-- Employee to Department. See "What I'd do differently" in the README for why
-- the original storing Department as free text is the schema's main flaw.

CREATE TABLE IF NOT EXISTS Department (
    DepartmentId   INTEGER PRIMARY KEY AUTOINCREMENT,
    DepartmentName TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Employee (
    EmployeeId    INTEGER PRIMARY KEY AUTOINCREMENT,
    EmployeeName  TEXT NOT NULL,
    Department    TEXT NOT NULL,
    DateOfJoining DATE NOT NULL,
    PhotoFileName TEXT NOT NULL DEFAULT 'anonymous.png'
);

CREATE INDEX IF NOT EXISTS IX_Employee_Department ON Employee (Department);
