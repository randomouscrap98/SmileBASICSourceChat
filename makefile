comp=mcs
run=mono

chat.exe: main.cs http.cs chat.cs auth.cs string.cs
	$(comp) -r:websocket-sharp.dll -r:Newtonsoft.Json.dll -out:chat.exe \
		main.cs http.cs chat.cs auth.cs string.cs

.PHONY: run
run: chat.exe
	$(run) chat.exe

.PHONY: clean
clean:
	rm -f *.exe
