# AzStorageDataMovementTest
Sample app using the Azure Storage Data Movement library used as a test bed before moving UWP code to Unity. This also includes a new method for progressive blob download by segments using only the standard Azure Storage library.

## Implementation Notes
* This project targets Windows 10 Fall Creators Update (build 16299) as a minimum since it uses a custom build of the Azure Storage Data Movement Library (DMLib) for UWP (derived from the .NET Standard 2.0 version).
* Make sure you add/fix a reference to the custom DMLib DLL includes in the CustomBinaries folder. or you can scrap that and simply use the standard DMLib from Nuget. 
* To test this project in Windows 10586, change the miniumum version to 10586, remove the DMLib dll from references, and remove the FALLCREATORSUPDATE pre-compiler definition in build settings. 
