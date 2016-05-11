#!/bin/sh

folder=ChatServer/ChatServer/bin/Release/
exe=ChatServer.exe
build=build.txt
dlls="ChatServer/ModulePackage1/bin/Release/ModulePackage1.dll ChatServer/PortedModules/bin/Release/PortedModules.dll"
serverFolder=server/

#Oops, some important files don't exist
if [ ! -f "$folder/$exe" ]
then
   echo "Missing ChatServer executable. Make sure it has been built"
   echo "using monodevelop."
   exit 1
#elif [ ! -f "$dlls" ]
#then
#   echo "Missing Module package dll. Make sure it has been built"
#   exit 1
fi

#Parse command line arguments
for a in "$@"
do
  if [ $a = "debug" ]
  then
     serverFolder=serverdebug/
     echo "Server will run in debug mode"
  fi
done

mkdir -p $serverFolder
cp $folder/*.dll $serverFolder
cp $folder/$exe $serverFolder/chat.exe
cp $folder/$build $serverFolder/$build
mkdir -p $serverFolder/plugins
cp $dlls $serverFolder/plugins
cd $serverFolder 

mono --gc=sgen ./chat.exe
exitCode=$?

echo "Exit code: $exitCode"
while [ $exitCode -ne 0 ] #-eq 99 ]
do
   date +%s > crash.txt
   echo "`date` : Code $exitCode" >> crashlog.txt

   if [ $exitCode -eq 99 ]; then
      echo "The server killed itself. Let's try to restart it in 3 seconds..."
   else
      echo "The server exited incorrectly. Restarting in 3 seconds!"
   fi

   sleep 3
   mono --gc=sgen ./chat.exe
   exitCode=$?
done

