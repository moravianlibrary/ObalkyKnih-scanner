@echo off
REM Compile admin setup file
%1\Compil32.exe /cc scannerClient_installer.iss
REM Compile NoAdmin setup file
%1\Compil32.exe /cc scannerClient_installer_noAdmin.iss
REM count SHA256 of Admin installer
for /f "tokens=1 delims= " %%a in ('sha256deep.exe Output/ObalkyKnih-scanner_setup.exe') do @set hashAdmin=%%a
REM set hashAdmin=%hashAdmin:~0,64%
REM count SHA256 of NoAdmin installer
for /f "tokens=1 delims= " %%a in ('sha256deep.exe Output/ObalkyKnih-scanner_setupNoAdmin.exe') do @set hashNoAdmin=%%a
REM get Version
for /f "tokens=1-2" %%i in ('sigcheck.exe Output\ObalkyKnih-scanner_setup.exe /accepteula') do ( if "%%i"=="Version:" set filever=%%j )
for /f "tokens=1,2 delims=." %%a in ("%filever%") do (set majorVersion=%%a)
for /f "tokens=2 delims=." %%a in ("%filever%") do (set minorVersion=%%a)
REM create XML
>Output\output.txt echo		^<latest-version^>
>>Output\output.txt echo			^<!-- Admin version --^>
>>Output\output.txt echo			^<version type="Admin"^>
>>Output\output.txt echo				^<major^>%majorVersion%^</major^>
>>Output\output.txt echo				^<minor^>%minorVersion%^</minor^>
>>Output\output.txt echo				^<date^>%date:~3%^</date^>
>>Output\output.txt echo				^<checksum^>%hashAdmin%^</checksum^>
>>Output\output.txt echo				^<download^>https://obalkyknih.cz/obalkyknih-scanner/obalkyknih-scanner_setup.exe^</download^>
>>Output\output.txt echo			^</version^>
>>Output\output.txt echo			^<!-- NoAdmin version --^>
>>Output\output.txt echo			^<version type="User"^>
>>Output\output.txt echo				^<major^>%majorVersion%^</major^>
>>Output\output.txt echo				^<minor^>%minorVersion%^</minor^>
>>Output\output.txt echo				^<date^>%date:~3%^</date^>
>>Output\output.txt echo				^<checksum^>%hashNoAdmin%^</checksum^>
>>Output\output.txt echo				^<download^>https://obalkyknih.cz/obalkyknih-scanner/obalkyknih-scanner_setupNoAdmin.exe^</download^>
>>Output\output.txt echo			^</version^>
>>Output\output.txt echo		^</latest-version^>