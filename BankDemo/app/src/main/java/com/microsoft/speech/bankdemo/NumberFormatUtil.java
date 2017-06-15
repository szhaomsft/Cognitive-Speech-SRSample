package com.microsoft.speech.bankdemo;

import java.util.HashMap;

/**
 * Created by v-guoya on 5/25/2017.
 */

public class NumberFormatUtil {
    public static HashMap<String, Long> chineseNumbers = new HashMap<String, Long>() {  // java verbosity. compare this to python's dict
        private static final long serialVersionUID = 1L;

        {
            put("零", 0L);
            put("一", 1L);
            put("壹", 1L);
            put("二", 2L);
            put("两", 2L);
            put("貳", 2L);
            put("贰", 2L);
            put("叁", 3L);
            put("參", 3L);
            put("三", 3L);
            put("肆", 4L);
            put("四", 4L);
            put("五", 5L);
            put("伍", 5L);
            put("陸", 6L);
            put("陆", 6L);
            put("六", 6L);
            put("柒", 7L);
            put("七", 7L);
            put("捌", 8L);
            put("八", 8L);
            put("九", 9L);
            put("玖", 9L);
            put("十", 10L);
            put("拾", 10L);
            put("佰", 100L);
            put("百", 100L);
            put("仟", 1000L);
            put("千", 1000L);
            put("万", 10000L);
            put("萬", 10000L);
            put("億", 100000000L);
            put("亿", 100000000L);
        }
    };

    public static String check = "零一壹二两貳贰叁參三肆四五伍陸陆六柒七捌八九玖十拾佰百仟千万萬億亿";

    public static long convert(String s) {
        int sLen = s.length();
        if (sLen == 0)
            return 0;

        String data = "";
        for(int i = 0; i < sLen; i++){
            if(check.indexOf(s.charAt(i)) != -1){
                data += s.charAt(i);
            }
        }
        sLen = data.length();
        s = data;
        if (sLen == 0)
            return 0;
        if (sLen > 1) {
            int pivot = 0; // index of the highest singular character value in the string
            for (int i = 0; i < sLen; i++)   // loop through the characters in the string to get the character with the highest value. That is your pivot
                if (convert(String.valueOf(s.charAt(i))) > convert(String.valueOf(s.charAt(pivot))))
                    pivot = i;
            long value = convert(String.valueOf(s.charAt(pivot)));
            long LHS, RHS;
            LHS = convert(s.substring(0, pivot));  // multiply value with LHS
            RHS = convert(s.substring(pivot + 1));  // add value with RHS
            if (LHS > 0)
                value *= LHS;
            value += RHS;
            return value;
        } else {
            return chineseNumbers.get(s).longValue();
        }
    }
}
