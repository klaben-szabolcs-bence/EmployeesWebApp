-- MS-SQL schema (the original target; still used for local development).
--
-- Note: CREATE DATABASE must be its own batch, hence the GO separators. Without
-- them this script fails in sqlcmd/SSMS, which is how it was originally written.
--
-- The SQLite equivalent used by the deployed demo is schema.sqlite.sql, which
-- also declares the primary keys and constraints this one is missing. See
-- "What I'd do differently" in the README.

CREATE DATABASE EmployeeDB
GO

USE EmployeeDB
GO

CREATE TABLE dbo.Department (
    DepartmentId   int identity(1,1),
    DepartmentName varchar(500)
)
GO

CREATE TABLE dbo.Employee (
    EmployeeId    int identity(1,1),
    EmployeeName  varchar(500),
    Department    varchar(500),
    DateOfJoining date,
    PhotoFileName varchar(500)
)
GO
