using System;
using System.Runtime.InteropServices;
using System.Drawing;
using FFmpeg.AutoGen;
using System.IO;
using System.Drawing.Imaging;
using System.Diagnostics;

namespace ObjDector
{
    internal static class FFmpegHelper
    {
        public static unsafe string av_strerror(int error)
        {
            var bufferSize = 1024;
            var buffer = stackalloc byte[bufferSize];
            ffmpeg.av_strerror(error, buffer, (ulong)bufferSize);
            var message = Marshal.PtrToStringAnsi((IntPtr)buffer);
            return message;
        }

        public static int ThrowExceptionIfError(this int error)
        {
            if (error < 0) throw new ApplicationException(av_strerror(error));
            return error;
        }
    }

    public unsafe class MediaFile : IDisposable
    {
        private unsafe readonly AVCodecContext* pCodecCtx;
        private unsafe readonly AVFormatContext* pFormatCtx;
        private unsafe readonly int streamIndex;
        private unsafe readonly AVFrame* pFrame;
        private unsafe readonly AVPacket* pPacket;
        private unsafe readonly SwsContext* scaleCtx;

        private IntPtr rgbFrameBuffer;
        private byte_ptrArray4 rgbBuf;
        private int_array4 dstLinesize;

        public string CodecName { get; }

        public int width { get; }
        public int height { get; }

        public long totalFrames { get; }

        public string filepath { get; }

        public MediaFile(String path, bool tryFrames = true)
        {
            pFormatCtx = ffmpeg.avformat_alloc_context();

            var formatCtx = pFormatCtx;
            ffmpeg.avformat_open_input(&formatCtx, path, null, null).ThrowExceptionIfError();

            ffmpeg.avformat_find_stream_info(pFormatCtx, null).ThrowExceptionIfError();

            AVStream* pStream = null;
            for (var i = 0; i < pFormatCtx->nb_streams; i++)
            {
                if (pFormatCtx->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    pStream = pFormatCtx->streams[i];
                    break;
                }
            }

            if (pStream == null)
            {
                throw new InvalidOperationException("Could not found video stream.");
            }

            streamIndex = pStream->index;
            pCodecCtx = pStream->codec;

            var codecId = pCodecCtx->codec_id;
            var pCodec = ffmpeg.avcodec_find_decoder(codecId);
            if (pCodec == null)
            {
                throw new InvalidOperationException("Unsupported codec.");
            }

            filepath = path;

            ffmpeg.avcodec_open2(pCodecCtx, pCodec, null).ThrowExceptionIfError();

            CodecName = ffmpeg.avcodec_get_name(codecId);

            pPacket = ffmpeg.av_packet_alloc();
            pFrame = ffmpeg.av_frame_alloc();

            width = pCodecCtx->width;
            height = pCodecCtx->height;
            totalFrames = tryFrames ? TryGetFrameCount() : pStream->nb_frames;

            var pixFmt = pCodecCtx->pix_fmt;
            if (pixFmt == AVPixelFormat.AV_PIX_FMT_NONE)
            {
                pixFmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
            }

            var destPixFmt = AVPixelFormat.AV_PIX_FMT_BGR24;

            scaleCtx = ffmpeg.sws_getContext(width, height, pixFmt,
                width, height, destPixFmt, 
                ffmpeg.SWS_BICUBIC, null, null, null);

            var rgbBufSize = ffmpeg.av_image_get_buffer_size(destPixFmt, width, height, 1);
            rgbFrameBuffer = Marshal.AllocHGlobal(rgbBufSize);

            rgbBuf = new byte_ptrArray4();
            dstLinesize = new int_array4();
            ffmpeg.av_image_fill_arrays(ref rgbBuf, ref dstLinesize, (byte*)rgbFrameBuffer, destPixFmt, width, height, 1);
        }

        private int TryGetFrameCount()
        {
            int nFrames = -1;
            var pStream = pFormatCtx->streams[streamIndex];

            try
            {
                if (ffmpeg.av_seek_frame(pFormatCtx, streamIndex, pStream->duration, ffmpeg.AVSEEK_FLAG_BACKWARD) < 0)
                {
                    ffmpeg.av_seek_frame(pFormatCtx, streamIndex, pStream->duration, ffmpeg.AVSEEK_FLAG_ANY).ThrowExceptionIfError();
                }

                ffmpeg.avcodec_flush_buffers(pStream->codec);

                AVFrame decoded;
                int f;
                while (true)
                {
                    f = TryDecodeNextFrame(out decoded);
                    if (f == -1)
                    {
                        break;
                    }
                    nFrames = f + 1;
                }
            }
            catch (Exception)
            {
                nFrames = -1;
            }

            if (ffmpeg.av_seek_frame(pFormatCtx, streamIndex, 0, ffmpeg.AVSEEK_FLAG_BACKWARD) < 0)
            {
                ffmpeg.av_seek_frame(pFormatCtx, streamIndex, 0, ffmpeg.AVSEEK_FLAG_ANY).ThrowExceptionIfError();
            }

            ffmpeg.avcodec_flush_buffers(pStream->codec);

            return nFrames;
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(rgbFrameBuffer);
            ffmpeg.sws_freeContext(scaleCtx);

            ffmpeg.av_frame_unref(pFrame);
            ffmpeg.av_free(pFrame);

            ffmpeg.av_packet_unref(pPacket);
            ffmpeg.av_free(pPacket);

            ffmpeg.avcodec_close(pCodecCtx);
            var formatCtx = pFormatCtx;
            ffmpeg.avformat_close_input(&formatCtx);
        }

        public bool GetFrame(long number, out Bitmap frame)
        {
            var fps = pFormatCtx->streams[streamIndex]->r_frame_rate;
            var timebase = pFormatCtx->streams[streamIndex]->time_base;

            long tc = Convert.ToInt64(number * (double)fps.den / fps.num / timebase.num * timebase.den);

            try
            {
                if (ffmpeg.av_seek_frame(pFormatCtx, streamIndex, tc, ffmpeg.AVSEEK_FLAG_BACKWARD) < 0)
                {
                    ffmpeg.av_seek_frame(pFormatCtx, streamIndex, tc, ffmpeg.AVSEEK_FLAG_ANY).ThrowExceptionIfError();
                }
            } catch
            {
                frame = null;
                return false;
            }

            ffmpeg.avcodec_flush_buffers(pFormatCtx->streams[streamIndex]->codec);

            long frameNum = -1;
            AVFrame decoded;
            while (frameNum != number)
            {
                frameNum = TryDecodeNextFrame(out decoded);
                if (frameNum == -1 || frameNum > number)
                {
                    frame = null;
                    return false;
                }
            }

            ffmpeg.sws_scale(scaleCtx, decoded.data, decoded.linesize, 0, height, rgbBuf, dstLinesize).ThrowExceptionIfError();

            // copy the bitmap to a new bitmap object, or all frame will share the same memory
            frame = new Bitmap(new Bitmap(width, height, dstLinesize[0], PixelFormat.Format24bppRgb, rgbFrameBuffer));
            return true;
        }

        public int GetNextFrame(out Bitmap frame)
        {
            int num;
            AVFrame decoded;
            frame = null;

            num = TryDecodeNextFrame(out decoded);
            if (num < 0)
            {
                return -1;
            }

            ffmpeg.sws_scale(scaleCtx, decoded.data, decoded.linesize, 0, height, rgbBuf, dstLinesize).ThrowExceptionIfError();
            
            // copy the bitmap to a new bitmap object, or all frame will share the same memory
            frame = new Bitmap(new Bitmap(width, height, dstLinesize[0], PixelFormat.Format24bppRgb, rgbFrameBuffer));
            return num;
        }

        public int TryDecodeNextFrame(out AVFrame frame)
        {
            ffmpeg.av_frame_unref(pFrame);
            int error;
            do
            {
                try
                {
                    do
                    {
                        error = ffmpeg.av_read_frame(pFormatCtx, pPacket);
                        if (error == ffmpeg.AVERROR_EOF)
                        {
                            frame = *pFrame;
                            return -1;
                        }

                        error.ThrowExceptionIfError();
                    } while (pPacket->stream_index != streamIndex);

                    ffmpeg.avcodec_send_packet(pCodecCtx, pPacket).ThrowExceptionIfError();
                }
                finally
                {
                    ffmpeg.av_packet_unref(pPacket);
                }

                error = ffmpeg.avcodec_receive_frame(pCodecCtx, pFrame);
            } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));

            error.ThrowExceptionIfError();
            frame = *pFrame;

            var fps = pFormatCtx->streams[streamIndex]->r_frame_rate;
            var timebase = pFormatCtx->streams[streamIndex]->time_base;
            var pts = pFrame->pts;

            int framenum = Convert.ToInt32((double)pts * timebase.num / timebase.den * fps.num / fps.den);
            Debug.WriteLine($"#{framenum} pts: {pts} timebase: {timebase.num}/{timebase.den} fps: {fps.num}/{fps.den}");

            return framenum;
        }
    }
}
