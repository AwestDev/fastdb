# Demo Login

The startup login is an intentionally weak, local demo gate. Its only credential pair is `admin` / `123`.
The verifier is compiled into the application, and neither the username nor password is persisted.

This gate is not real authentication and does not secure database data, profile files, or credentials held
in the running process. Database access still depends on SQL Server authentication and permissions. Do
not present this demo login as a security feature.
