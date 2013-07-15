REM Compile setup file
%1\Compil32.exe /cc scannerClient_installer.iss
REM Create Hash
sha256deep.exe Output/ObalkyKnih-scanner_setup.exe > checksum.txt