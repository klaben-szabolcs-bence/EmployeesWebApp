-- Demo rows. The container filesystem on a free tier is ephemeral, so this runs
-- on every cold start where the database comes up empty. That is the documented
-- steady state, not a failure: the demo is always populated and a visitor's
-- edits never persist.

INSERT INTO Department (DepartmentName) VALUES
    ('Engineering'),
    ('Human Resources'),
    ('Sales'),
    ('Support');

INSERT INTO Employee (EmployeeName, Department, DateOfJoining, PhotoFileName) VALUES
    ('Anna Kovács',    'Engineering',     '2021-03-01', 'anonymous.png'),
    ('Bence Tóth',     'Engineering',     '2022-09-15', 'anonymous.png'),
    ('Csilla Nagy',    'Human Resources', '2020-01-20', 'anonymous.png'),
    ('Dániel Szabó',   'Sales',           '2023-06-05', 'anonymous.png'),
    ('Eszter Horváth', 'Support',         '2024-02-12', 'anonymous.png');
