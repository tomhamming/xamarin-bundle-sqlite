# SQLite transaction timeouts and Xamarin

This is an example project to demonstrate an issue with transactions and busy timeouts in SQLite under Xamarin. The idea is:

 - Open a database connection and configure it as WAL journaling with a 10 second (or so) transaction timeout.
 - Start a transaction and do some nontrivial work (simulated as a sleep in this case)
 - On another thread, open another connection with the same configuration to the same DB file. Open a transaction.
 
Expected behavior is that the second transaction should block until the first one finishes, or return `SQLITE_BUSY` if the first transaction lasts longer than the second one's timeout value. This is what happens under Xamarin on the simulator, and under Objective-C on both the simulator and device.

But under Xamarin on the device, the second transaction fails with `SQLITE_BUSY` immediately.

Both the projects (Xcode and Xamarin) have SQLite bundled with them. This is because I've tried to upgrade my linked SQLite version as part of my efforts to debug this issue. But I can't get Xamarin to link against it.

# Update for iOS 9.2

As of iOS 9.2, running the sample app and linking against the built-in SQLite in iOS displays the deadlock issue on both simulator and device under both Xamarin and Xcode. But if you build SQLite 3.8.10.2 (which is what is built into iOS 9 currently) yourself and link against it from Xcode, everything works. So it looks like the only way to get around this is by bundling a custom-built versio of SQLite. This is [supposed to be possible] with Xamarin, but I can't get it to work. Right now the libot_sqlite included in the Xamarin project is 3.9.2.
