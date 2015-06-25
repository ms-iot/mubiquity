
MCU = atmega32u4
F_CPU=16000000L
DEF_CPU=AVR_LEONARDO
VARIANT=leonardo
D_USB_VID=USB_VID=0x2341
D_USB_PID=USB_PID=0x8036

TARGET=Testing

O = obj

ARDUINO_INSTALL = c:\Program Files (x86)\Arduino
ARDUINO_FIRMWARE = $(ARDUINO_INSTALL)\hardware\arduino\avr\cores\arduino
ARDUINO_TOOLS = $(ARDUINO_INSTALL)\hardware\tools\avr\avr\bin
VARIANT_PATH=$(ARDUINO_INSTALL)\hardware\arduino\avr\variants\leonardo

CXXSRC = \
    $(PROJ_CXXSRC) \
	"$(ARDUINO_FIRMWARE)\abi.cpp"					\
	"$(ARDUINO_FIRMWARE)\CDC.cpp"					\
	"$(ARDUINO_FIRMWARE)\HardwareSerial.cpp"		\
	"$(ARDUINO_FIRMWARE)\HardwareSerial0.cpp"		\
	"$(ARDUINO_FIRMWARE)\HardwareSerial1.cpp"		\
	"$(ARDUINO_FIRMWARE)\HardwareSerial2.cpp"		\
	"$(ARDUINO_FIRMWARE)\HardwareSerial3.cpp"		\
	"$(ARDUINO_FIRMWARE)\HID.cpp"					\
	"$(ARDUINO_FIRMWARE)\IPAddress.cpp"				\
	"$(ARDUINO_FIRMWARE)\main.cpp"					\
	"$(ARDUINO_FIRMWARE)\new.cpp"					\
	"$(ARDUINO_FIRMWARE)\Print.cpp"					\
	"$(ARDUINO_FIRMWARE)\Stream.cpp"				\
	"$(ARDUINO_FIRMWARE)\Tone.cpp"					\
	"$(ARDUINO_FIRMWARE)\USBCore.cpp"				\
	"$(ARDUINO_FIRMWARE)\WMath.cpp"					\
	"$(ARDUINO_FIRMWARE)\WString.cpp"

CSRC = \
    $(PROJ_SRC) \
	"$(ARDUINO_FIRMWARE)\hooks.c"					\
	"$(ARDUINO_FIRMWARE)\WInterrupts.c"				\
	"$(ARDUINO_FIRMWARE)\wiring.c"					\
	"$(ARDUINO_FIRMWARE)\wiring_analog.c"			\
	"$(ARDUINO_FIRMWARE)\wiring_digital.c"			\
	"$(ARDUINO_FIRMWARE)\wiring_pulse.c"			\
	"$(ARDUINO_FIRMWARE)\wiring_shift.c"

INOSRC=\
	"Arduino.ino"

FORMAT = ihex

CFLAGS = -gstabs -include Arduino.h -DF_CPU=$(F_CPU) -D$(DEF_CPU) -D$(D_USB_VID) -D$(D_USB_PID) -I "$(ARDUINO_FIRMWARE)" -I "$(VARIANT_PATH)" -Os
CXXFLAGS = -gstabs -include Arduino.h -DF_CPU=$(F_CPU) -D$(DEF_CPU) -D$(D_USB_VID) -D$(D_USB_PID) -I "$(ARDUINO_FIRMWARE)" -I "$(VARIANT_PATH)" -Os -fno-exceptions
LDFLAGS = -lm -Xlinker --allow-multiple-definition

# Program settings
CC = "$(ARDUINO_TOOLS)\g++"
CXX = "$(ARDUINO_TOOLS)\g++"
LD = "$(ARDUINO_TOOLS)\ld"
OBJCOPY = "$(ARDUINO_TOOLS)\objcopy"

OBJ = $(CSRC:.c=.o) $(CXXSRC:.cpp=.o) $(INOSRC:.ino=.o)

# Define all listing files.
LST = $(ASRC:.S=.lst) $(CXXSRC:.cpp=.lst) $(CSRC:.c=.lst)

# Combine all necessary flags and optional flags.
# Add target processor to flags.
ALL_CFLAGS = -mmcu=$(MCU) -I. $(CFLAGS)
ALL_CXXFLAGS = -mmcu=$(MCU) -I. $(CXXFLAGS)

# Default target.
all: build

build: elf hex

elf: $(TARGET).elf
hex: $(TARGET).hex

.SUFFIXES: .elf .hex

# Link: create ELF output file from object files.
$(TARGET).elf: $(OBJ)
	$(CC) $(ALL_CFLAGS) $(OBJ) --output $@ $(LDFLAGS)

$(TARGET).hex: $(TARGET).elf
	$(OBJCOPY) -O $(FORMAT) -R .eeprom $(TARGET).elf $@

# Compile: create object files from C++ source files.
.cpp.o:
	$(CXX) -c $(ALL_CXXFLAGS) $< -o $@ 

# Compile: create object files from C++ source files.
.ino.o:
	$(CXX) -c $(ALL_CXXFLAGS) $< -o $@ 

# Compile: create object files from C source files.
.c.o:
	$(CC) -c $(ALL_CFLAGS) $< -o $@ 


# Target: clean project.
clean:
	-del /f $(TARGET).hex $(TARGET).elf $(TARGET).map
	rd /s /q $(O)

.PHONY:	all build elf hex clean