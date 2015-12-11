using System;

using UIKit;
using Foundation;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace SqlTester
{
    public partial class ViewController : UIViewController
    {
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        static extern int sqlite3_open_v2(IntPtr filename, out IntPtr db, int flags, IntPtr vfs);
        static int sqlite3_open_v2(string filename, out IntPtr db, int flags, IntPtr vfs)
        {
            var bytes = Encoding.UTF8.GetBytes(filename);
            var ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.AllocHGlobal(bytes.Length + 1);
                Marshal.Copy(bytes, 0, ptr, bytes.Length);
                Marshal.WriteByte(ptr, bytes.Length, 0);
                return sqlite3_open_v2(ptr, out db, flags, vfs);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        static extern int sqlite3_prepare16_v2(IntPtr db, [MarshalAs(UnmanagedType.LPWStr)] string pSql, int nBytes, out IntPtr stmt, out IntPtr ptrRemain);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        static extern int sqlite3_step(IntPtr stmt);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        static extern int sqlite3_close(IntPtr db);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        static extern int sqlite3_finalize(IntPtr stmt);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        static extern IntPtr sqlite3_errmsg16(IntPtr db);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int sqlite3_busy_timeout(IntPtr db, int ms);

        internal const int SQLITE_ERROR = 1;
        internal const int SQLITE_OK = 0;
        internal const int SQLITE_CANTOPEN = 14;
        internal const int SQLITE_BUSY = 5;
        internal const int SQLITE_ROW = 100;
        internal const int SQLITE_DONE = 101;
        /* sqlite3_step() has finished executing */

        internal const int SQLITE_IOERR = 10;
        internal const int SQLITE_IOERR_BLOCKED = (SQLITE_IOERR | (11 << 8));

        internal const int SQLITE_OPEN_READONLY = 0x00000001;
        internal const int SQLITE_OPEN_READWRITE = 0x00000002;
        internal const int SQLITE_OPEN_CREATE = 0x00000004;
        internal const int SQLITE_OPEN_URI = 0x00000040;
        internal const int SQLITE_OPEN_MEMORY = 0x00000080;
        internal const int SQLITE_OPEN_NOMUTEX = 0x00008000;
        internal const int SQLITE_OPEN_FULLMUTEX = 0x00010000;
        internal const int SQLITE_OPEN_SHAREDCACHE = 0x00020000;
        internal const int SQLITE_OPEN_PRIVATECACHE = 0x00080000;

        internal const int SQLITE_INTEGER = 1;
        internal const int SQLITE_FLOAT = 2;
        internal const int SQLITE_BLOB = 4;
        internal const int SQLITE_NULL = 5;
        internal const int SQLITE_TEXT = 3;
        internal const int SQLITE3_TEXT = 3;

        public ViewController(IntPtr handle) : base(handle)
        {
        }
        
        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            // Perform any additional setup after loading the view, typically from a nib.

            UIButton btn = new UIButton(UIButtonType.RoundedRect);
            btn.SetTitle("Break Transactions", UIControlState.Normal);
            btn.Frame = new CoreGraphics.CGRect(100, 100, 150, 30);
            btn.AddTarget(this, new ObjCRuntime.Selector("onClick"), UIControlEvent.TouchUpInside);
            View.AddSubview(btn);
        }

        [Export("onClick")]
        private void onClick()
        {
            var directories = NSSearchPath.GetDirectories(NSSearchPathDirectory.DocumentDirectory, NSSearchPathDomain.User, true);
            var dirName = directories[0];
            var dbName = dirName + "/test.sqlite";

            var mode = SQLITE_OPEN_FULLMUTEX | SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE;

            var mre1 = new ManualResetEvent(false);

            var trans1 = Task.Run(() =>
            {
                Debug.WriteLine("Start first thread. Opening.");
                var db1 = IntPtr.Zero;
                var err = sqlite3_open_v2(dbName, out db1, mode, IntPtr.Zero);
                if (!_checkError(err, db1))
                    return;
                Debug.WriteLine("First opened");

                if (!_executeStmt("PRAGMA journal_mode='WAL'", db1))
                    return;

                if (!_checkError(sqlite3_busy_timeout(db1, 10000), db1))
                    return;

                Debug.WriteLine("Begin first");
                if (!_executeStmt("BEGIN IMMEDIATE TRANSACTION", db1))
                    return;
                Debug.WriteLine("First is begun");

                mre1.Set();

                //simulate something long-running in the database work
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(5));

                Debug.WriteLine("End first");
                if (!_executeStmt("COMMIT", db1))
                    return;

                sqlite3_close(db1);
            });

            var trans2 = Task.Run(() =>
            {
                mre1.WaitOne();

                Debug.WriteLine("Start second thread. Opening.");
                var db2 = IntPtr.Zero;
                var err = sqlite3_open_v2(dbName, out db2, mode, IntPtr.Zero);
                if (!_checkError(err, db2))
                    return;
                Debug.WriteLine("Second opened");

                if (!_executeStmt("PRAGMA journal_mode='WAL'", db2))
                    return;

                if (!_checkError(sqlite3_busy_timeout(db2, 10000), db2))
                    return;

                //Beginning this transaction should block until the first transaction is committed.
                //On the simulator this works. On the device it fails instantly.
                Debug.WriteLine("Begin second");
                if (!_executeStmt("BEGIN IMMEDIATE TRANSACTION", db2))
                    return;

                //You shouldn't see this log until the first transaction above is committed
                Debug.WriteLine("Second is begun");

                Debug.WriteLine("End second");
                if (!_executeStmt("COMMIT", db2))
                    return;

                sqlite3_close(db2);
            });

            Task.Run(() =>
            {
                Task.WaitAll(trans1, trans2);
                Debug.WriteLine("Finished");
            });
        }

        private bool _executeStmt(string statement, IntPtr db)
        {
            IntPtr garbage;
            IntPtr stmt;

            var stmtErr = sqlite3_prepare16_v2(db, statement, -1, out stmt, out garbage);

            if (!_checkError(stmtErr, db))
                return false;

            stmtErr = sqlite3_step(stmt);
            if (!_checkError(stmtErr, db))
                return false;

            stmtErr = sqlite3_finalize(stmt);
            if (!_checkError(stmtErr, db))
                return false;

            return true;
        }

        private bool _checkError(int result, IntPtr db)
        {
            if (result != SQLITE_OK && result != SQLITE_DONE && result != SQLITE_ROW)
            {
                var errMsg = Marshal.PtrToStringUni(sqlite3_errmsg16(db));
                Debug.WriteLine($"Error: {errMsg}");
                return false;
            }

            return true;
        }

        public override void DidReceiveMemoryWarning()
        {
            base.DidReceiveMemoryWarning();
            // Release any cached data, images, etc that aren't in use.
        }
    }
}

