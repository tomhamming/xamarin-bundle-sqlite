xcodebuild -project ot_sqlite.xcodeproj -target ot_sqlite -sdk iphonesimulator -arch x86_64 -configuration Release clean build
mv build/Release-iphonesimulator/libot_sqlite.a build/libot_sqlite.x86_64.a

xcodebuild -project ot_sqlite.xcodeproj -target ot_sqlite -sdk iphonesimulator -arch i386 -configuration Release clean build
mv build/Release-iphonesimulator/libot_sqlite.a build/libot_sqlite.i386.a

xcodebuild -project ot_sqlite.xcodeproj -target ot_sqlite -sdk iphoneos -arch arm64 -configuration Release clean build
mv build/Release-iphoneos/libot_sqlite.a build/libot_sqlite.arm64.a

xcodebuild -project ot_sqlite.xcodeproj -target ot_sqlite -sdk iphoneos -arch armv7 -configuration Release clean build
mv build/Release-iphoneos/libot_sqlite.a build/libot_sqlite.armv7.a

cd build

lipo -create -output libot_sqlite.a libot_sqlite.i386.a libot_sqlite.x86_64.a libot_sqlite.arm64.a libot_sqlite.armv7.a
mv -f libot_sqlite.a ../../libot_sqlite.a
cd ..
rm -r build