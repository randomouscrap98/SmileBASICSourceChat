#!/bin/sh

folder=ChatServer/ChatServer/bin/Debug/
exe=ChatServer.exe
dlls=ChatServer/ModulePackage1/bin/Debug/ModulePackage1.dll
serverFolder=server/

#Oops, some important files don't exist
if [ ! -f "$folder/$exe" ]
then
   echo "Missing ChatServer executable. Make sure it has been built"
   echo "using monodevelop."
   exit 1
elif [ ! -f $dlls ]
then
   echo "Missing Module package dll. Make sure it has been built"
   exit 1
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
mkdir -p $serverFolder/plugins
cp $dlls $serverFolder/plugins
cd $serverFolder 
./chat.exe

