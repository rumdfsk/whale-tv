package cn.whaletv.mobile;
import org.junit.Test;
import static org.junit.Assert.*;
public class ChannelTest {
    @Test public void onlyOfficialStreams() {
        assertTrue(Channel.ALL[0].accepts("https://zwebl02.cztv.com/live/channel01720Pnew.m3u8?auth_key=x"));
        assertFalse(Channel.ALL[0].accepts("https://evilcztv.com/live/channel01720Pnew.m3u8"));
        assertFalse(Channel.ALL[0].accepts("http://zwebl02.cztv.com/live/channel01720Pnew.m3u8"));
        assertFalse(Channel.ALL[0].accepts("https://zwebl02.cztv.com/live/channel02720Pnew.m3u8"));
        assertFalse(Channel.ALL[0].accepts("https://cztv.com.evil.test/live/channel01720Pnew.m3u8"));
        assertFalse(Channel.ALL[0].accepts("not a URL"));
        assertFalse(Channel.ALL[3].accepts("https://example.com/live.m3u8"));
    }
    @Test public void channelBoundaries() {
        assertEquals(7,Channel.wrap(-1)); assertEquals(0,Channel.wrap(8));
        assertEquals(7,Channel.wrap(-17));
    }
}
