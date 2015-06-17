
MCU = atmega32u4
F_CPU=16000000L
DEF_CPU=AVR_LEONARDO
VARIANT=leonardo
D_USB_VID=USB_VID=0x2341
D_USB_PID=USB_PID=0x8036

TARGET=$(Output)

O = $(IntDir)

ARDUINO_INSTALL = c:\PROGRA~2\Arduino
ARDUINO_FIRMWARE = $(ARDUINO_INSTALL)\hardware\arduino\avr\cores\arduino
ARDUINO_TOOLS = $(ARDUINO_INSTALL)\hardware\tools\avr\bin
VARIANT_PATH=$(ARDUINO_INSTALL)\hardware\arduino\avr\variants\leonardo

CXXSRC = \
	abi.cpp					\
	CDC.cpp					\
	HardwareSerial.cpp		\
	HardwareSerial0.cpp		\
	HardwareSerial1.cpp		\
	HardwareSerial2.cpp		\
	HardwareSerial3.cpp		\
	HID.cpp					\
	IPAddress.cpp				\
	main.cpp					\
	new.cpp					\
	Print.cpp					\
	Stream.cpp				\
	Tone.cpp					\
	USBCore.cpp				\
	WMath.cpp					\
	WString.cpp

CSRC = \
	hooks.c					\
	WInterrupts.c				\
	wiring.c					\
	wiring_analog.c			\
	wiring_digital.c			\
	wiring_pulse.c			\
	wiring_shift.c

INOSRC=\
	Arduino.ino

FORMAT = ihex

CFLAGS = -g -Os -w -ffunction-sections -fdata-sections -MMD -gstabs -DF_CPU=$(F_CPU) -D$(DEF_CPU) -D$(D_USB_VID) -D$(D_USB_PID) -I "$(ARDUINO_FIRMWARE)" -I "$(VARIANT_PATH)"
CXXFLAGS = -g -Os -w -fno-exceptions -ffunction-sections -fdata-sections -fno-threadsafe-statics -MMD -DF_CPU=$(F_CPU) -D$(DEF_CPU) -D$(D_USB_VID) -D$(D_USB_PID) -I "$(ARDUINO_FIRMWARE)" -I "$(VARIANT_PATH)"
LDFLAGS = -Os -Wl,--gc-sections

# Program settings
CC = "$(ARDUINO_TOOLS)\avr-gcc.exe"
CXX = "$(ARDUINO_TOOLS)\avr-g++.exe"
LD = "$(ARDUINO_TOOLS)\avr-ld.exe"
OBJCOPY = "$(ARDUINO_TOOLS)\avr-objcopy.exe"

# OBJ = $(CSRC:.c=.obj) $(CXXSRC:.cpp=.obj) $(INOSRC:.ino=.obj)
OBJ = \
	$(O)\abi.obj					\
	$(O)\CDC.obj					\
	$(O)\HardwareSerial.obj			\
	$(O)\HardwareSerial0.obj		\
	$(O)\HardwareSerial1.obj		\
	$(O)\HardwareSerial2.obj		\
	$(O)\HardwareSerial3.obj		\
	$(O)\HID.obj					\
	$(O)\IPAddress.obj				\
	$(O)\main.obj					\
	$(O)\new.obj					\
	$(O)\Print.obj					\
	$(O)\Stream.obj					\
	$(O)\Tone.obj					\
	$(O)\USBCore.obj				\
	$(O)\WMath.obj					\
	$(O)\WString.obj				\
	$(O)\hooks.obj					\
	$(O)\WInterrupts.obj			\
	$(O)\wiring.obj					\
	$(O)\wiring_analog.obj			\
	$(O)\wiring_digital.obj			\
	$(O)\wiring_pulse.obj			\
	$(O)\wiring_shift.obj			\
	$(O)\Arduino.obj



# Combine all necessary flags and optional flags.
# Add target processor to flags.
ALL_CFLAGS = -mmcu=$(MCU) -I. $(CFLAGS)
ALL_CXXFLAGS = -mmcu=$(MCU) -I. $(CXXFLAGS)

# Default target.
all: build

build: elf hex

elf: $(TARGET).elf
hex: $(TARGET)

.SUFFIXES: .elf .hex .ino

# Link: create ELF output file from object files.
$(TARGET).elf: $(OBJ)
	$(CC) $(ALL_CFLAGS) $(OBJ) --output $@ $(LDFLAGS)

$(TARGET): $(TARGET).elf
	$(OBJCOPY) -O $(FORMAT) -R .eeprom $(TARGET).elf $@

# Compile: create object files from C++ source files.
{$(ARDUINO_FIRMWARE)}.cpp{$(O)}.obj:
	$(CXX) -c $(ALL_CXXFLAGS) $< -o $@

# Compile: create object files from C++ source files.
.ino{$(O)}.obj:
	$(CXX) -x c++ -include "$(ARDUINO_FIRMWARE)\Arduino.h" -c $(ALL_CXXFLAGS) $< -o $@ 

# Compile: create object files from C source files.
{$(ARDUINO_FIRMWARE)}.c{$(O)}.obj:
	$(CC) -c $(ALL_CFLAGS) $< -o $@ 


# Target: clean project.
clean:
	-del /f $(TARGET).hex $(TARGET).elf $(TARGET).map
	rd /s /q $(O)

.PHONY:	all build elf hex clean