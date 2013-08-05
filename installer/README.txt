3 steps of creating and publishing of a new version

First, you need to open ScannerClient-obalkyknih.sln in Visual C#,
INCREMENT ASSEMBLY VERSION AND FILE VERSION (Properties->Application->Assembly Information...),
and build the project.

Second, run auto-compile.bat from command line with parameter of your
Inno Setup installation.
It should looks like auto-compile.bat "C:\Program Files (x86)\Inno Setup 5"

Third, copy all executable files (.exe) from Output into folder obalkyknih-scanner on server
and edit update-info.xml on server by replacing latest-version tag with the text generated
in output.txt file.

Done