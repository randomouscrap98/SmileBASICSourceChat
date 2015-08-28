#!/bin/sh

exe=ChatServer/ChatServer/bin/Debug/ChatServer.exe

if [ ! -f $exe ]
then
   echo "Missing ChatServer executable. Make sure it has been built"
   echo "using monodevelop."
   exit 1
fi

cp *.dll server
cp $exe server/chat.exe
./server/chat.exe

