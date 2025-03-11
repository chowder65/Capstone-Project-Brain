FROM nvidia/cuda:12.6.1-cudnn-devel-ubuntu22.04

COPY ./LM_Studio* /data/lms/
COPY ./keyboard /etc/default/keyboard
COPY ./http-server-config.json /http-server-config.json
RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y keyboard-configuration console-setup tzdata dbus x11-utils x11-xserver-utils libgl1-mesa-glx
RUN dpkg-reconfigure -f noninteractive keyboard-configuration
RUN apt-get update &&  apt-get install -y libfuse2 kmod fuse libglib2.0-0 libnss3 libatk1.0-0 libatk-bridge2.0-0 libcups2 libdrm2 libgtk-3-0 libgbm1 libasound2 xserver-xorg xvfb x11vnc
RUN mkdir -p /root/.vnc && x11vnc -storepasswd <password> /root/.vnc/passwd
ENV DISPLAY=:99
RUN chmod ugo+x /data/lms/*.AppImage
RUN /data/lms/*.AppImage --appimage-extract
RUN cd /squashfs-root/
RUN chown root:root /squashfs-root/chrome-sandbox
RUN chmod 4755 /squashfs-root/chrome-sandbox
COPY start_services.sh /start_services.sh
RUN chmod +x /start_services.sh

CMD ["/start_services.sh"]