3 steps of creating and publishing of a new version

First, you need to build the project and create all binary files
by opening ScannerClient-obalkyknih.sln and building it in Visual C#

Second, run auto-compile.bat from command line with parameter of your
Inno Setup installation.
It should looks like auto-compile.bat "C:\Program Files (x86)\Inno Setup 5"

Then copy ObalkyKnih-scanner_setup into server and edit update-info.xml
on server by copying checksum from file checksum.txt into checksum tag
in update-info.xml

Done

