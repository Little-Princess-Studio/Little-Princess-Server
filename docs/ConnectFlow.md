# Connect Flow

1. Client connect to gate, the first package must be `Authentication`, if not, gate will shut up this connection;
2. If authentication successes (using RSA), gate will random select a server to create an `Untrusted` entity which will send the `MailBox` to each gate, then each gate will record the mapping between remote socket and `MailBox` and notify the client to create the same `MailBox` with `CreateMailBox` package, (A `MailBox` is the unique way to access an entity in server);
3. Then the client will use `MailBox` to access the related entity on the server by `EntityRPC` Package.