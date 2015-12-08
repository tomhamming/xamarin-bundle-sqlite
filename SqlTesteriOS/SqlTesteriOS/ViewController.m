//
//  ViewController.m
//  SqlTesteriOS
//
//  Created by Hamming, Tom on 12/7/15.
//  Copyright © 2015 Olive Tree Bible Software. All rights reserved.
//

#import "ViewController.h"
#include <sqlite3.h>

@interface ViewController ()

@end

@implementation ViewController

- (void)viewDidLoad {
    [super viewDidLoad];
    // Do any additional setup after loading the view, typically from a nib.
    NSLog(@"Startup sqlite version %s", sqlite3_libversion());
}

- (void)didReceiveMemoryWarning {
    [super didReceiveMemoryWarning];
    // Dispose of any resources that can be recreated.
}

-(void)onBreak
{
    NSArray *directories = NSSearchPathForDirectoriesInDomains(NSDocumentDirectory, NSUserDomainMask, YES);
    NSString *dir = directories.firstObject;
    NSString *dbName = [dir stringByAppendingPathComponent:@"test.sqlite"];
    
    int mode = SQLITE_OPEN_FULLMUTEX | SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE;
    
    dispatch_queue_t queue = dispatch_queue_create("testQueue", DISPATCH_QUEUE_CONCURRENT);
    
    dispatch_async(queue, ^
                   {
                       NSLog(@"Start first thread. Opening.");
                       const char *pathAndFileName = [dbName UTF8String];
                       sqlite3 *db = NULL;
                       int err = sqlite3_open_v2(pathAndFileName, &db, mode, NULL);
                       if (![self _checkError:err withDatabase:db])
                           return;
                       NSLog(@"First opened");
                       
                       if (![self _executeStmt:@"PRAGMA journal_mode='WAL'" withDatabase:db])
                           return;
                       
                       if (![self _checkError:sqlite3_busy_timeout(db, 10000) withDatabase:db])
                           return;
                       
                       NSLog(@"Begin first");
                       if (![self _executeStmt:@"BEGIN IMMEDIATE TRANSACTION" withDatabase:db])
                           return;
                       NSLog(@"First is begun");
                       
                       [NSThread sleepForTimeInterval:5];
                       
                       NSLog(@"End first");
                       if (![self _executeStmt:@"COMMIT" withDatabase:db])
                           return;
                       
                       sqlite3_close(db);
                   });
    
    dispatch_async(queue, ^
                   {
                       [NSThread sleepForTimeInterval:0.2];
                       
                       NSLog(@"Start second thread. Opening.");
                       const char *pathAndFileName = [dbName UTF8String];
                       sqlite3 *db = NULL;
                       int err = sqlite3_open_v2(pathAndFileName, &db, mode, NULL);
                       if (![self _checkError:err withDatabase:db])
                           return;
                       NSLog(@"Second opened");
                       
                       if (![self _executeStmt:@"PRAGMA journal_mode='WAL'" withDatabase:db])
                           return;
                       
                       if (![self _checkError:sqlite3_busy_timeout(db, 10000) withDatabase:db])
                           return;
                       
                       NSLog(@"Begin second");
                       if (![self _executeStmt:@"BEGIN IMMEDIATE TRANSACTION" withDatabase:db])
                           return;
                       NSLog(@"Second is begun");
                       
                       NSLog(@"End second");
                       if (![self _executeStmt:@"COMMIT" withDatabase:db])
                           return;
                       
                       sqlite3_close(db);
                   });
    
    dispatch_barrier_async(queue, ^
                          {
                              NSLog(@"All finished");
                          });
}

-(BOOL)_executeStmt:(NSString *)statement withDatabase:(sqlite3 *)db
{
    NSData *utf16 = [statement dataUsingEncoding:NSUTF16LittleEndianStringEncoding];
    sqlite3_stmt *stmt = NULL;
    int err = sqlite3_prepare16_v2(db, [utf16 bytes], (int)[utf16 length], &stmt, NULL);
    if (![self _checkError:err withDatabase:db])
        return NO;
    
    err = sqlite3_step(stmt);
    if (![self _checkError:err withDatabase:db])
        return NO;
    
    err = sqlite3_finalize(stmt);
    if (![self _checkError:err withDatabase:db])
        return NO;
    
    return YES;
}

-(BOOL)_checkError:(int)err withDatabase:(sqlite3 *)db
{
    if (err != SQLITE_OK && err != SQLITE_DONE && err != SQLITE_ROW)
    {
        NSString *errMsg = [NSString stringWithFormat:@"%S", (const unichar *)sqlite3_errmsg16(db)];
        NSLog(@"Error: %@", errMsg);
        return NO;
    }
    
    return YES;
}

@end
